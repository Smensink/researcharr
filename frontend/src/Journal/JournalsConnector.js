import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchJournals, setJournalsSort } from 'Store/Actions/journalActions';
import Journals from './Journals';

function createMapStateToProps() {
  return createSelector(
    (state) => state.journals,
    (journals) => {
      return {
        ...journals
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onFetchJournals() {
      dispatch(fetchJournals());
    },
    onSortPress(sortKey) {
      dispatch(setJournalsSort({ sortKey }));
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(Journals);
