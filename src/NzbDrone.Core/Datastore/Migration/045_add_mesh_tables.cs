using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(045)]
    public class add_mesh_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("MeshDescriptors")
                  .WithColumn("DescriptorUi").AsString().NotNullable().Indexed()
                  .WithColumn("PreferredTerm").AsString().Nullable()
                  .WithColumn("TreeNumbers").AsString().Nullable()
                  .WithColumn("ScopeNote").AsString().Nullable();

            Create.TableForModel("MeshTerms")
                  .WithColumn("DescriptorUi").AsString().NotNullable().Indexed()
                  .WithColumn("Term").AsString().NotNullable()
                  .WithColumn("IsPreferred").AsBoolean().NotNullable();

            // Seed a few common terms to make the feature usable without an importer.
            Insert.IntoTable("MeshDescriptors").Row(new { DescriptorUi = "D006331", PreferredTerm = "Heart Failure", TreeNumbers = "C14.280.400.715", ScopeNote = "A clinical syndrome..." });
            Insert.IntoTable("MeshDescriptors").Row(new { DescriptorUi = "D003920", PreferredTerm = "Diabetes Mellitus", TreeNumbers = "C18.452.394.750", ScopeNote = "A heterogeneous group of disorders..." });
            Insert.IntoTable("MeshDescriptors").Row(new { DescriptorUi = "D001943", PreferredTerm = "Critical Care", TreeNumbers = "N02.278.421.250", ScopeNote = "Health care provided to patients with life-threatening conditions." });

            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D006331", Term = "Heart Failure", IsPreferred = true });
            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D006331", Term = "Cardiac Failure", IsPreferred = false });
            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D006331", Term = "Congestive Heart Failure", IsPreferred = false });

            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D003920", Term = "Diabetes Mellitus", IsPreferred = true });
            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D003920", Term = "Type 2 Diabetes", IsPreferred = false });
            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D003920", Term = "NIDDM", IsPreferred = false });

            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D001943", Term = "Critical Care", IsPreferred = true });
            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D001943", Term = "Intensive Care", IsPreferred = false });
            Insert.IntoTable("MeshTerms").Row(new { DescriptorUi = "D001943", Term = "ICU", IsPreferred = false });
        }
    }
}
