import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { grabQueueItem, removeQueueItem } from 'Store/Actions/queueActions';
import createAuthorSelector from 'Store/Selectors/createAuthorSelector';
import createBookSelector from 'Store/Selectors/createBookSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import QueueRow from './QueueRow';

const authorSelector = createAuthorSelector();
const bookSelector = createBookSelector();

function createMapStateToProps() {
  return createSelector(
    (state, props) => {
      // Try to get author from selector if authorId exists
      if (props.authorId) {
        try {
          return authorSelector(state, props) || props.author || null;
        } catch (e) {
          // If selector fails (e.g., author not in state), use embedded author
          return props.author || null;
        }
      }
      // Otherwise use embedded author if available
      return props.author || null;
    },
    (state, props) => {
      // Try to get book from selector if bookId exists
      if (props.bookId) {
        try {
          return bookSelector(state, props) || props.book || null;
        } catch (e) {
          // If selector fails (e.g., book not in state), use embedded book
          return props.book || null;
        }
      }
      // Otherwise use embedded book if available
      return props.book || null;
    },
    createUISettingsSelector(),
    (author, book, uiSettings) => {
      const result = _.pick(uiSettings, [
        'showRelativeDates',
        'shortDateFormat',
        'timeFormat'
      ]);

      result.author = author;
      result.book = book;

      return result;
    }
  );
}

const mapDispatchToProps = {
  grabQueueItem,
  removeQueueItem
};

class QueueRowConnector extends Component {

  //
  // Listeners

  onGrabPress = () => {
    this.props.grabQueueItem({ id: this.props.id });
  };

  onRemoveQueueItemPress = (payload) => {
    this.props.removeQueueItem({ id: this.props.id, ...payload });
  };

  //
  // Render

  render() {
    return (
      <QueueRow
        {...this.props}
        onGrabPress={this.onGrabPress}
        onRemoveQueueItemPress={this.onRemoveQueueItemPress}
      />
    );
  }
}

QueueRowConnector.propTypes = {
  id: PropTypes.number.isRequired,
  book: PropTypes.object,
  grabQueueItem: PropTypes.func.isRequired,
  removeQueueItem: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(QueueRowConnector);
