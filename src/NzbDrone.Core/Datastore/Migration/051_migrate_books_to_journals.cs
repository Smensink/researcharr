using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FluentMigrator;
using FluentMigrator.Builders.Execute;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(51)]
    public class MigrateBooksToJournals : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add a column to track if a book needs journal migration (for books that couldn't be migrated automatically)
            Alter.Table("Books").AddColumn("NeedsJournalMigration").AsBoolean().WithDefaultValue(false);

            // Step 1: Find all books that have person authors (Type = 0 or null, and Disambiguation != "Journal")
            // We'll try to migrate them to journals based on Edition.Disambiguation field
            
            // First, create a temporary table to track journal metadata we need to create
            Create.Table("_TempJournalMigration")
                .WithColumn("JournalName").AsString().NotNullable()
                .WithColumn("AuthorMetadataId").AsInt32().Nullable()
                .WithColumn("BookId").AsInt32().NotNullable();

            // Find books with person authors and try to extract journal from edition disambiguation
            // Use database-agnostic approach: insert one row per book-journal combination
            IfDatabase("sqlite").Execute.Sql(@"
                INSERT INTO ""_TempJournalMigration"" (""JournalName"", ""BookId"")
                SELECT DISTINCT 
                    e.""Disambiguation"" as JournalName,
                    b.""Id"" as BookId
                FROM ""Books"" b
                INNER JOIN ""AuthorMetadata"" am ON b.""AuthorMetadataId"" = am.""Id""
                INNER JOIN ""Editions"" e ON e.""BookId"" = b.""Id""
                WHERE (am.""Type"" IS NULL OR am.""Type"" = 0)
                    AND am.""Disambiguation"" IS NOT 'Journal'
                    AND e.""Disambiguation"" IS NOT NULL
                    AND e.""Disambiguation"" != ''
                    AND e.""Disambiguation"" != am.""Name"";
            ");

            IfDatabase("postgres").Execute.Sql(@"
                INSERT INTO ""_TempJournalMigration"" (""JournalName"", ""BookId"")
                SELECT DISTINCT 
                    e.""Disambiguation"" as JournalName,
                    b.""Id"" as BookId
                FROM ""Books"" b
                INNER JOIN ""AuthorMetadata"" am ON b.""AuthorMetadataId"" = am.""Id""
                INNER JOIN ""Editions"" e ON e.""BookId"" = b.""Id""
                WHERE (am.""Type"" IS NULL OR am.""Type"" = 0)
                    AND am.""Disambiguation"" IS DISTINCT FROM 'Journal'
                    AND e.""Disambiguation"" IS NOT NULL
                    AND e.""Disambiguation"" != ''
                    AND e.""Disambiguation"" != am.""Name"";
            ");

            // Step 2: For each unique journal name, create or find journal metadata
            // We'll create journal metadata entries with Type = 1 (Journal)
            // Use a simpler approach that works on both SQLite and PostgreSQL
            IfDatabase("sqlite").Execute.Sql(@"
                INSERT INTO ""AuthorMetadata"" (""ForeignAuthorId"", ""TitleSlug"", ""Name"", ""SortName"", ""NameLastFirst"", ""SortNameLastFirst"", ""Disambiguation"", ""Type"", ""Status"", ""Links"", ""Genres"", ""Aliases"", ""Images"")
                SELECT DISTINCT
                    'journal_migration_' || LOWER(REPLACE(REPLACE(REPLACE(t.""JournalName"", ' ', '_'), '-', '_'), '.', '_')) || '_' || CAST(t.""BookId"" AS TEXT) as ForeignAuthorId,
                    'journal_migration_' || LOWER(REPLACE(REPLACE(REPLACE(t.""JournalName"", ' ', '_'), '-', '_'), '.', '_')) || '_' || CAST(t.""BookId"" AS TEXT) as TitleSlug,
                    t.""JournalName"" as Name,
                    LOWER(t.""JournalName"") as SortName,
                    t.""JournalName"" as NameLastFirst,
                    LOWER(t.""JournalName"") as SortNameLastFirst,
                    'Journal' as Disambiguation,
                    1 as Type,
                    0 as Status,
                    '[]' as Links,
                    '[]' as Genres,
                    '[]' as Aliases,
                    '[]' as Images
                FROM ""_TempJournalMigration"" t
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""AuthorMetadata"" am2 
                    WHERE am2.""Name"" = t.""JournalName"" 
                        AND (am2.""Type"" = 1 OR am2.""Disambiguation"" = 'Journal')
                )
                GROUP BY t.""JournalName"";
            ");

            IfDatabase("postgres").Execute.Sql(@"
                INSERT INTO ""AuthorMetadata"" (""ForeignAuthorId"", ""TitleSlug"", ""Name"", ""SortName"", ""NameLastFirst"", ""SortNameLastFirst"", ""Disambiguation"", ""Type"", ""Status"", ""Links"", ""Genres"", ""Aliases"", ""Images"")
                SELECT DISTINCT ON (t.""JournalName"")
                    'journal_migration_' || LOWER(REPLACE(REPLACE(REPLACE(t.""JournalName"", ' ', '_'), '-', '_'), '.', '_')) || '_' || CAST(MIN(t.""BookId"") OVER (PARTITION BY t.""JournalName"") AS TEXT) as ForeignAuthorId,
                    'journal_migration_' || LOWER(REPLACE(REPLACE(REPLACE(t.""JournalName"", ' ', '_'), '-', '_'), '.', '_')) || '_' || CAST(MIN(t.""BookId"") OVER (PARTITION BY t.""JournalName"") AS TEXT) as TitleSlug,
                    t.""JournalName"" as Name,
                    LOWER(t.""JournalName"") as SortName,
                    t.""JournalName"" as NameLastFirst,
                    LOWER(t.""JournalName"") as SortNameLastFirst,
                    'Journal' as Disambiguation,
                    1 as Type,
                    0 as Status,
                    '[]' as Links,
                    '[]' as Genres,
                    '[]' as Aliases,
                    '[]' as Images
                FROM ""_TempJournalMigration"" t
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""AuthorMetadata"" am2 
                    WHERE am2.""Name"" = t.""JournalName"" 
                        AND (am2.""Type"" = 1 OR am2.""Disambiguation"" = 'Journal')
                );
            ");

            // Step 3: Update the temp table with the created/found journal metadata IDs
            IfDatabase("sqlite").Execute.Sql(@"
                UPDATE ""_TempJournalMigration""
                SET ""AuthorMetadataId"" = (
                    SELECT am.""Id""
                    FROM ""AuthorMetadata"" am
                    WHERE am.""Name"" = ""_TempJournalMigration"".""JournalName""
                        AND (am.""Type"" = 1 OR am.""Disambiguation"" = 'Journal')
                    LIMIT 1
                )
                WHERE ""AuthorMetadataId"" IS NULL;
            ");

            IfDatabase("postgres").Execute.Sql(@"
                UPDATE ""_TempJournalMigration"" t
                SET ""AuthorMetadataId"" = am.""Id""
                FROM ""AuthorMetadata"" am
                WHERE am.""Name"" = t.""JournalName""
                    AND (am.""Type"" = 1 OR am.""Disambiguation"" = 'Journal')
                    AND t.""AuthorMetadataId"" IS NULL;
            ");

            // Step 4: Update books to point to journal metadata instead of person author metadata
            IfDatabase("sqlite").Execute.Sql(@"
                UPDATE ""Books""
                SET ""AuthorMetadataId"" = (
                    SELECT t.""AuthorMetadataId""
                    FROM ""_TempJournalMigration"" t
                    WHERE t.""BookId"" = ""Books"".""Id""
                        AND t.""AuthorMetadataId"" IS NOT NULL
                    LIMIT 1
                ),
                ""NeedsJournalMigration"" = 0
                WHERE ""Id"" IN (
                    SELECT DISTINCT t.""BookId""
                    FROM ""_TempJournalMigration"" t
                    WHERE t.""AuthorMetadataId"" IS NOT NULL
                )
                AND ""AuthorMetadataId"" != (
                    SELECT t.""AuthorMetadataId""
                    FROM ""_TempJournalMigration"" t
                    WHERE t.""BookId"" = ""Books"".""Id""
                    LIMIT 1
                );
            ");

            IfDatabase("postgres").Execute.Sql(@"
                UPDATE ""Books"" b
                SET ""AuthorMetadataId"" = t.""AuthorMetadataId"",
                    ""NeedsJournalMigration"" = false
                FROM ""_TempJournalMigration"" t
                WHERE b.""Id"" = t.""BookId""
                    AND t.""AuthorMetadataId"" IS NOT NULL
                    AND b.""AuthorMetadataId"" != t.""AuthorMetadataId"";
            ");

            // Step 5: Mark books that couldn't be migrated (no journal info in edition disambiguation)
            // These will need to be refetched to get journal information
            IfDatabase("sqlite").Execute.Sql(@"
                UPDATE ""Books""
                SET ""NeedsJournalMigration"" = 1
                WHERE ""Id"" IN (
                    SELECT DISTINCT b2.""Id""
                    FROM ""Books"" b2
                    INNER JOIN ""AuthorMetadata"" am ON b2.""AuthorMetadataId"" = am.""Id""
                    LEFT JOIN ""Editions"" e ON e.""BookId"" = b2.""Id"" AND e.""Disambiguation"" IS NOT NULL AND e.""Disambiguation"" != ''
                    WHERE (am.""Type"" IS NULL OR am.""Type"" = 0)
                        AND am.""Disambiguation"" IS NOT 'Journal'
                        AND (e.""Id"" IS NULL OR e.""Disambiguation"" = '' OR e.""Disambiguation"" = am.""Name"")
                        AND b2.""NeedsJournalMigration"" = 0
                );
            ");

            IfDatabase("postgres").Execute.Sql(@"
                UPDATE ""Books"" b
                SET ""NeedsJournalMigration"" = true
                WHERE b.""Id"" IN (
                    SELECT DISTINCT b2.""Id""
                    FROM ""Books"" b2
                    INNER JOIN ""AuthorMetadata"" am ON b2.""AuthorMetadataId"" = am.""Id""
                    LEFT JOIN ""Editions"" e ON e.""BookId"" = b2.""Id"" AND e.""Disambiguation"" IS NOT NULL AND e.""Disambiguation"" != ''
                    WHERE (am.""Type"" IS NULL OR am.""Type"" = 0)
                        AND am.""Disambiguation"" IS DISTINCT FROM 'Journal'
                        AND (e.""Id"" IS NULL OR e.""Disambiguation"" = '' OR e.""Disambiguation"" = am.""Name"")
                        AND b2.""NeedsJournalMigration"" = false
                );
            ");

            // Clean up temporary table
            Delete.Table("_TempJournalMigration");
        }
    }
}

