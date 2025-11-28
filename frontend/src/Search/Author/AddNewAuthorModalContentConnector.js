import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { addAuthor, setAuthorAddDefault } from 'Store/Actions/searchActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createSystemStatusSelector from 'Store/Selectors/createSystemStatusSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import AddNewAuthorModalContent from './AddNewAuthorModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search,
    (state) => state.settings.metadataProfiles,
    (state) => state.settings.qualityProfiles,
    createDimensionsSelector(),
    createSystemStatusSelector(),
    (searchState, metadataProfiles, qualityProfiles, dimensions, systemStatus) => {
      const {
        isAdding,
        addError,
        authorDefaults
      } = searchState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(authorDefaults, {}, addError);

      const qualityProfileItems = (qualityProfiles && qualityProfiles.items) || [];
      const currentQualityProfileId = settings && settings.qualityProfileId ? settings.qualityProfileId.value : 0;
      const defaultQualityProfileId = currentQualityProfileId || (qualityProfileItems[0] && qualityProfileItems[0].id) || 0;

      return {
        isAdding,
        addError,
        showMetadataProfile: false, // NONE (not allowed for authors) and one other
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        isWindows: systemStatus.isWindows,
        ...settings,
        qualityProfileId: {
          ...settings.qualityProfileId,
          value: defaultQualityProfileId
        }
      };
    }
  );
}

const mapDispatchToProps = {
  setAuthorAddDefault,
  addAuthor
};

class AddNewAuthorModalContentConnector extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setAuthorAddDefault({ [name]: value });
  };

  onAddAuthorPress = (searchForMissingBooks) => {
    const {
      foreignAuthorId,
      rootFolderPath,
      monitor,
      monitorNewItems,
      qualityProfileId,
      metadataProfileId,
      tags
    } = this.props;

    this.props.addAuthor({
      foreignAuthorId,
      rootFolderPath: rootFolderPath.value,
      monitor: monitor.value,
      monitorNewItems: monitorNewItems.value,
      qualityProfileId: qualityProfileId.value,
      metadataProfileId: metadataProfileId.value,
      tags: tags.value,
      searchForMissingBooks
    });
  };

  //
  // Render

  render() {
    return (
      <AddNewAuthorModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddAuthorPress={this.onAddAuthorPress}
      />
    );
  }
}

AddNewAuthorModalContentConnector.propTypes = {
  foreignAuthorId: PropTypes.string.isRequired,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  monitorNewItems: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  tags: PropTypes.object.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setAuthorAddDefault: PropTypes.func.isRequired,
  addAuthor: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewAuthorModalContentConnector);
