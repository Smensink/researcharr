import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { setQueueOption } from 'Store/Actions/queueActions';
import QueueOptions from './QueueOptions';

function createMapStateToProps() {
  return createSelector(
    (state) => state.queue && state.queue.options,
    (options) => options || { includeUnknownAuthorItems: false }
  );
}

const mapDispatchToProps = {
  onOptionChange: setQueueOption
};

export default connect(createMapStateToProps, mapDispatchToProps)(QueueOptions);
