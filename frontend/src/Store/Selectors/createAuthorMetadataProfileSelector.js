import { createSelector } from 'reselect';
import createAuthorSelector from './createAuthorSelector';

function createAuthorMetadataProfileSelector() {
  return createSelector(
    (state) => state.settings?.metadataProfiles?.items || [],
    createAuthorSelector(),
    (metadataProfiles, author) => {
      if (!author || !author.metadataProfileId) {
        return undefined;
      }
      return metadataProfiles.find((profile) => {
        return profile.id === author.metadataProfileId;
      });
    }
  );
}

export default createAuthorMetadataProfileSelector;
