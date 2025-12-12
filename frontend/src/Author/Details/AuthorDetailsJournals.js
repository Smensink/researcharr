import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import { icons } from 'Helpers/Props';
import styles from './AuthorDetailsJournals.css';

const columns = [
  {
    name: 'journal',
    label: 'Journal',
    isVisible: true
  },
  {
    name: 'paperCount',
    label: 'Papers',
    isVisible: true
  },
  {
    name: 'citations',
    label: 'Total Citations',
    isVisible: true
  },
  {
    name: 'monitored',
    label: 'Monitored Papers',
    isVisible: true
  }
];

class AuthorDetailsJournals extends Component {

  //
  // Render

  render() {
    const { books } = this.props;

    // Group books by journal
    const journalMap = {};

    books.forEach((book) => {
      const journal = book.journal || 'Unknown Journal';

      if (!journalMap[journal]) {
        journalMap[journal] = {
          name: journal,
          papers: [],
          totalCitations: 0,
          monitoredCount: 0
        };
      }

      journalMap[journal].papers.push(book);
      journalMap[journal].totalCitations += (book.citations || 0);
      if (book.monitored) {
        journalMap[journal].monitoredCount++;
      }
    });

    // Convert to array and sort by paper count
    const journals = Object.values(journalMap).sort((a, b) => b.papers.length - a.papers.length);

    if (journals.length === 0) {
      return (
        <div className={styles.noJournals}>
          No journal information available for this researcher's papers.
        </div>
      );
    }

    return (
      <FieldSet legend="Publications by Journal">
        <Table
          columns={columns}
        >
          <TableBody>
            {
              journals.map((journal) => {
                return (
                  <TableRow key={journal.name}>
                    <TableRowCell>
                      <div className={styles.journalName}>
                        <Icon
                          name={icons.BOOK}
                          size={14}
                        />
                        {' '}
                        {journal.name}
                      </div>
                    </TableRowCell>

                    <TableRowCell>
                      <span className={styles.paperCount}>
                        {journal.papers.length}
                      </span>
                      {' paper'}
                      {journal.papers.length !== 1 ? 's' : ''}
                    </TableRowCell>

                    <TableRowCell>
                      {journal.totalCitations > 0 ? (
                        <span title={`${journal.totalCitations} total citations`}>
                          📊 {journal.totalCitations}
                        </span>
                      ) : (
                        <span className={styles.noCitations}>—</span>
                      )}
                    </TableRowCell>

                    <TableRowCell>
                      <span>
                        {journal.monitoredCount} of {journal.papers.length}
                      </span>
                    </TableRowCell>
                  </TableRow>
                );
              })
            }
          </TableBody>
        </Table>

        <div className={styles.journalSummary}>
          <strong>{journals.length}</strong> journal{journals.length !== 1 ? 's' : ''} total
        </div>
      </FieldSet>
    );
  }
}

AuthorDetailsJournals.propTypes = {
  books: PropTypes.arrayOf(PropTypes.object).isRequired
};

export default AuthorDetailsJournals;
