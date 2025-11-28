import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TablePager from 'Components/Table/TablePager';
import TextInput from 'Components/Form/TextInput';
import { sortDirections } from 'Helpers/Props';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import getToggledRange from 'Utilities/Table/getToggledRange';
import BookRowConnector from './BookRowConnector';
import styles from './AuthorDetailsSeason.css';

class AuthorDetailsSeason extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      lastToggledBook: null,
      page: 1,
      pageSize: 100,
      filter: ''
    };
  }

  componentDidMount() {
    this.props.setSelectedState(this.props.items);
  }

  onFilterChange = ({ value }) => {
    this.setState({ filter: value, page: 1 });
  };

  componentDidUpdate(prevProps) {
    const {
      items,
      sortKey,
      sortDirection,
      setSelectedState
    } = this.props;

    if (sortKey !== prevProps.sortKey ||
        sortDirection !== prevProps.sortDirection ||
        hasDifferentItemsOrOrder(prevProps.items, items)
    ) {
      setSelectedState(items);

      const totalPages = Math.max(1, Math.ceil(items.length / this.state.pageSize));
      if (this.state.page > totalPages) {
        this.setState({ page: totalPages });
      }
    }
  }

  onFirstPagePress = () => {
    this.setState({ page: 1 });
  };

  onPreviousPagePress = () => {
    this.setState((state) => ({ page: Math.max(1, state.page - 1) }));
  };

  onNextPagePress = () => {
    this.setState((state) => {
      const totalPages = Math.max(1, Math.ceil(this.props.items.length / state.pageSize));
      return { page: Math.min(totalPages, state.page + 1) };
    });
  };

  onLastPagePress = () => {
    const totalPages = Math.max(1, Math.ceil(this.props.items.length / this.state.pageSize));
    this.setState({ page: totalPages });
  };

  onPageSelect = (page) => {
    const totalPages = Math.max(1, Math.ceil(this.props.items.length / this.state.pageSize));
    this.setState({ page: Math.min(totalPages, Math.max(1, page)) });
  };

  //
  // Listeners

  onMonitorBookPress = (bookId, monitored, { shiftKey }) => {
    const lastToggled = this.state.lastToggledBook;
    const bookIds = [bookId];

    if (shiftKey && lastToggled) {
      const { lower, upper } = getToggledRange(this.props.items, bookId, lastToggled);
      const items = this.props.items;

      for (let i = lower; i < upper; i++) {
        bookIds.push(items[i].id);
      }
    }

    this.setState({ lastToggledBook: bookId });

    this.props.onMonitorBookPress(_.uniq(bookIds), monitored);
  };

  onSelectedChange = ({ id, value, shiftKey = false }) => {
    const {
      onSelectedChange,
      items
    } = this.props;

    return onSelectedChange(items, id, value, shiftKey);
  };

  //
  // Render

  render() {
    const {
      items,
      isEditorActive,
      columns,
      sortKey,
      sortDirection,
      onSortPress,
      onTableOptionChange,
      selectedState
    } = this.props;

    const { page, pageSize, filter } = this.state;

    const lowerFilter = filter.trim().toLowerCase();
    const filteredItems = lowerFilter.length === 0
      ? items
      : items.filter((item) => {
          const title = item.title?.toLowerCase() || '';
          const authorName = item.author?.name?.toLowerCase() || '';
          const topics = (item.topics || item.genres || []).join(' ').toLowerCase();
          return title.includes(lowerFilter) ||
            authorName.includes(lowerFilter) ||
            topics.includes(lowerFilter);
        });

    const totalPages = Math.max(1, Math.ceil(filteredItems.length / pageSize));
    const start = (page - 1) * pageSize;
    const end = start + pageSize;
    const pagedItems = filteredItems.slice(start, end);

    let titleColumns = columns;
    if (!isEditorActive) {
      titleColumns = columns.filter((x) => x.name !== 'select');
    }

    return (
      <div
        className={styles.bookType}
      >
        <div className={styles.filterRow}>
          <TextInput
            name="filter"
            autoComplete="off"
            placeholder="Filter by title, author, or topic..."
            value={filter}
            onChange={this.onFilterChange}
          />
        </div>

        <div className={styles.books}>
          <Table
            columns={titleColumns}
            sortKey={sortKey}
            sortDirection={sortDirection}
            onSortPress={onSortPress}
            onTableOptionChange={onTableOptionChange}
          >
            <TableBody>
              {
                pagedItems.map((item) => {
                  return (
                    <BookRowConnector
                      key={item.id}
                      columns={columns}
                      {...item}
                      onMonitorBookPress={this.onMonitorBookPress}
                      isEditorActive={isEditorActive}
                      isSelected={selectedState[item.id]}
                      onSelectedChange={this.onSelectedChange}
                    />
                  );
                })
              }
            </TableBody>
          </Table>
        </div>
        <div className={styles.pager}>
          <TablePager
            page={page}
            totalPages={totalPages}
            totalRecords={items.length}
            isFetching={false}
            onFirstPagePress={this.onFirstPagePress}
            onPreviousPagePress={this.onPreviousPagePress}
            onNextPagePress={this.onNextPagePress}
            onLastPagePress={this.onLastPagePress}
            onPageSelect={this.onPageSelect}
          />
        </div>
      </div>
    );
  }
}

AuthorDetailsSeason.propTypes = {
  sortKey: PropTypes.string,
  sortDirection: PropTypes.oneOf(sortDirections.all),
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  isEditorActive: PropTypes.bool.isRequired,
  selectedState: PropTypes.object.isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  onTableOptionChange: PropTypes.func.isRequired,
  onExpandPress: PropTypes.func.isRequired,
  setSelectedState: PropTypes.func.isRequired,
  onSelectedChange: PropTypes.func.isRequired,
  onSortPress: PropTypes.func.isRequired,
  onMonitorBookPress: PropTypes.func.isRequired,
  uiSettings: PropTypes.object.isRequired
};

export default AuthorDetailsSeason;
