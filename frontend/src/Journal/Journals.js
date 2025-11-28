import PropTypes from 'prop-types';
import React, { Component } from 'react';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import JournalsRow from './JournalsRow';

class Journals extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.onFetchJournals();
  }

  //
  // Render

  render() {
    const {
      isFetching,
      isPopulated,
      error,
      items,
      columns,
      sortKey,
      sortDirection,
      onSortPress
    } = this.props;

    return (
      <PageContent title="Journals">
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('Refresh')}
              iconName={icons.REFRESH}
              onPress={this.props.onFetchJournals}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody>
          {
            isFetching && !isPopulated &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <div>Unable to load journals</div>
          }

          {
            isPopulated && !error && !items.length &&
              <div>No journals found</div>
          }

          {
            isPopulated && !error && items.length > 0 &&
              <Table
                columns={columns}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={onSortPress}
              >
                <TableBody>
                  {
                    items.map((item) => {
                      return (
                        <JournalsRow
                          key={item.id}
                          {...item}
                          columns={columns}
                        />
                      );
                    })
                  }
                </TableBody>
              </Table>
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

Journals.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  sortKey: PropTypes.string,
  sortDirection: PropTypes.string,
  onSortPress: PropTypes.func.isRequired,
  onFetchJournals: PropTypes.func.isRequired
};

export default Journals;
