import React, { Component } from 'react';
import Button from 'Components/Link/Button';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
// import MetadatasConnector from './Metadata/MetadatasConnector';
import MetadataProviderConnector from './MetadataProvider/MetadataProviderConnector';

class MetadataSettings extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._saveCallback = null;

    this.state = {
      isSaving: false,
      hasPendingChanges: false,
      meshFile: null,
      meshStatus: ''
    };
  }

  //
  // Listeners

  onChildMounted = (saveCallback) => {
    this._saveCallback = saveCallback;
  };

  onChildStateChange = (payload) => {
    this.setState(payload);
  };

  onSavePress = () => {
    if (this._saveCallback) {
      this._saveCallback();
    }
  };

  //
  // Render
  render() {
    const {
      isSaving,
      hasPendingChanges,
      meshFile
    } = this.state;

    const onImportMeshUrl = () => {
      this.setState({ meshStatus: 'Importing MeSH...' });
      const req = createAjaxRequest({
        url: '/mesh/import?url=https://nlmpubs.nlm.nih.gov/projects/mesh/MESH_FILES/xmlmesh/desc2025.xml',
        method: 'GET'
      }).request;

      req.then(() => {
        this.setState({ meshStatus: 'MeSH import complete.' });
      }).fail((xhr) => {
        const detail = xhr?.responseJSON?.message ||
          xhr?.responseJSON?.errors &&
            Object.values(xhr.responseJSON.errors).flat().join('; ') ||
          xhr?.responseText ||
          xhr?.statusText ||
          'Unknown error';
        this.setState({ meshStatus: `MeSH import failed: ${detail}` });
      });
    };

    const onPickMeshFile = (e) => {
      if (!e.target.files || !e.target.files[0]) {
        this.setState({ meshFile: null });
        return;
      }
      this.setState({ meshFile: e.target.files[0] });
    };

    const onImportMeshFile = () => {
      if (!meshFile) {
        return;
      }
      this.setState({ meshStatus: 'Importing MeSH from file...' });
      const formData = new window.FormData();
      formData.append('file', meshFile);
      const req = createAjaxRequest({
        url: '/mesh/import',
        method: 'POST',
        data: formData,
        processData: false,
        contentType: false
      }).request;

      req.then(() => {
        this.setState({ meshStatus: 'MeSH import complete.' });
      }).fail((xhr) => {
        const detail = xhr?.responseJSON?.message ||
          xhr?.responseJSON?.errors &&
            Object.values(xhr.responseJSON.errors).flat().join('; ') ||
          xhr?.responseText ||
          xhr?.statusText ||
          'Unknown error';
        this.setState({ meshStatus: `MeSH import failed: ${detail}` });
      });
    };

    return (
      <PageContent title={translate('MetadataSettings')}>
        <SettingsToolbarConnector
          isSaving={isSaving}
          hasPendingChanges={hasPendingChanges}
          onSavePress={this.onSavePress}
        />

        <PageContentBody>
          <MetadataProviderConnector
            onChildMounted={this.onChildMounted}
            onChildStateChange={this.onChildStateChange}
          />
          <div style={{ marginTop: 12 }}>
            <div style={{ marginBottom: 8 }}>
              <Button onPress={onImportMeshUrl}>
                Import latest MeSH descriptors (download)
              </Button>
            </div>
            <div>
              <label>
                Import MeSH from local XML:
                <input type="file" accept=".xml"
                  onChange={onPickMeshFile}
                />
              </label>
              <div style={{ marginTop: 8 }}>
                <Button onPress={onImportMeshFile} disabled={!meshFile}>
                  Import selected file
                </Button>
              </div>
            </div>
            {this.state.meshStatus && (
              <div style={{ marginTop: 8 }}>
                {this.state.meshStatus}
              </div>
            )}
          </div>
          {/* <MetadatasConnector /> */}
        </PageContentBody>
      </PageContent>
    );
  }
}

export default MetadataSettings;
