import PropTypes from 'prop-types';
import React, { lazy, Suspense } from 'react';
import { Redirect, Route } from 'react-router-dom';
import Switch from 'Components/Router/Switch';
import SpinnerIcon from 'Components/SpinnerIcon';
import { icons } from 'Helpers/Props';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';

const BlocklistConnector = lazy(() => import('Activity/Blocklist/BlocklistConnector'));
const HistoryConnector = lazy(() => import('Activity/History/HistoryConnector'));
const QueueConnector = lazy(() => import('Activity/Queue/QueueConnector'));
const AuthorDetailsPageConnector = lazy(() => import('Author/Details/AuthorDetailsPageConnector'));
const AuthorIndexConnector = lazy(() => import('Author/Index/AuthorIndexConnector'));
const BookDetailsPageConnector = lazy(() => import('Book/Details/BookDetailsPageConnector'));
const BookIndexConnector = lazy(() => import('Book/Index/BookIndexConnector'));
const BookshelfConnector = lazy(() => import('Bookshelf/BookshelfConnector'));
const JournalsConnector = lazy(() => import('Journal/JournalsConnector'));
const CalendarPageConnector = lazy(() => import('Calendar/CalendarPageConnector'));
const NotFound = lazy(() => import('Components/NotFound'));
const AddNewItemConnector = lazy(() => import('Search/AddNewItemConnector'));
const CustomFormatSettingsConnector = lazy(() => import('Settings/CustomFormats/CustomFormatSettingsConnector'));
const DevelopmentSettingsConnector = lazy(() => import('Settings/Development/DevelopmentSettingsConnector'));
const DownloadClientSettingsConnector = lazy(() => import('Settings/DownloadClients/DownloadClientSettingsConnector'));
const GeneralSettingsConnector = lazy(() => import('Settings/General/GeneralSettingsConnector'));
const ImportListSettingsConnector = lazy(() => import('Settings/ImportLists/ImportListSettingsConnector'));
const IndexerSettingsConnector = lazy(() => import('Settings/Indexers/IndexerSettingsConnector'));
const IndexerErrorsConnector = lazy(() => import('Settings/Indexers/Errors/IndexerErrorsConnector'));
const MediaManagementConnector = lazy(() => import('Settings/MediaManagement/MediaManagementConnector'));
const MetadataSettings = lazy(() => import('Settings/Metadata/MetadataSettings'));
const NotificationSettings = lazy(() => import('Settings/Notifications/NotificationSettings'));
const Profiles = lazy(() => import('Settings/Profiles/Profiles'));
const QualityConnector = lazy(() => import('Settings/Quality/QualityConnector'));
const Settings = lazy(() => import('Settings/Settings'));
const TagSettings = lazy(() => import('Settings/Tags/TagSettings'));
const UISettingsConnector = lazy(() => import('Settings/UI/UISettingsConnector'));
const BackupsConnector = lazy(() => import('System/Backup/BackupsConnector'));
const LogsTableConnector = lazy(() => import('System/Events/LogsTableConnector'));
const Logs = lazy(() => import('System/Logs/Logs'));
const Status = lazy(() => import('System/Status/Status'));
const Tasks = lazy(() => import('System/Tasks/Tasks'));
const Updates = lazy(() => import('System/Updates/Updates'));
const UnmappedFilesTableConnector = lazy(() => import('UnmappedFiles/UnmappedFilesTableConnector'));
const CutoffUnmetConnector = lazy(() => import('Wanted/CutoffUnmet/CutoffUnmetConnector'));
const MissingConnector = lazy(() => import('Wanted/Missing/MissingConnector'));
const ImportSearches = lazy(() => import('ImportSearches/ImportSearches'));
const AdvancedSearch = lazy(() => import('AdvancedSearch/AdvancedSearch'));

function AppRoutes(props) {
  const {
    app
  } = props;

  return (
    <Suspense
      fallback={
        <div
          style={{
            display: 'flex',
            justifyContent: 'center',
            alignItems: 'center',
            height: '100vh'
          }}
        >
          <SpinnerIcon name={icons.SPINNER} isSpinning={true} />
        </div>
      }
    >
      <Switch>
        {/*
        Author
      */}

        <Route
          exact={true}
          path="/"
          component={AuthorIndexConnector}
        />

        {
          window.Researcharr.urlBase &&
            <Route
              exact={true}
              path="/"
              addUrlBase={false}
              render={() => {
                return (
                  <Redirect
                    to={getPathWithUrlBase('/')}
                    component={app}
                  />
                );
              }}
            />
        }

        <Route
          path="/authors"
          component={AuthorIndexConnector}
        />

        <Route
          path="/add/search"
          component={AddNewItemConnector}
        />

        <Route
          exact={true}
          path="/shelf"
          component={BookshelfConnector}
        />

        <Route
          exact={true}
          path="/books"
          component={BookIndexConnector}
        />

        <Route
          exact={true}
          path="/journals"
          component={JournalsConnector}
        />

        <Route
          path="/unmapped"
          component={UnmappedFilesTableConnector}
        />

        <Route
          path="/search-imports"
          component={ImportSearches}
        />

        <Route
          path="/advanced-search"
          component={AdvancedSearch}
        />

        <Route
          path="/author/:titleSlug"
          component={AuthorDetailsPageConnector}
        />

        <Route
          path="/book/:titleSlug"
          component={BookDetailsPageConnector}
        />

        {/*
        Calendar
      */}

        <Route
          path="/calendar"
          component={CalendarPageConnector}
        />

        {/*
        Activity
      */}

        <Route
          path="/activity/history"
          component={HistoryConnector}
        />

        <Route
          path="/activity/queue"
          component={QueueConnector}
        />

        <Route
          path="/activity/blocklist"
          component={BlocklistConnector}
        />

        {/*
        Wanted
      */}

        <Route
          path="/wanted/missing"
          component={MissingConnector}
        />

        <Route
          path="/wanted/cutoffunmet"
          component={CutoffUnmetConnector}
        />

        {/*
        Settings
      */}

        <Route
          exact={true}
          path="/settings"
          component={Settings}
        />

        <Route
          path="/settings/mediamanagement"
          component={MediaManagementConnector}
        />

        <Route
          path="/settings/profiles"
          component={Profiles}
        />

        <Route
          path="/settings/quality"
          component={QualityConnector}
        />

        <Route
          path="/settings/customformats"
          component={CustomFormatSettingsConnector}
        />

        <Route
          exact={true}
          path="/settings/indexers"
          component={IndexerSettingsConnector}
        />

        <Route
          path="/settings/indexers/:id/errors"
          component={IndexerErrorsConnector}
        />

        <Route
          path="/settings/downloadclients"
          component={DownloadClientSettingsConnector}
        />

        <Route
          path="/settings/importlists"
          component={ImportListSettingsConnector}
        />

        <Route
          path="/settings/connect"
          component={NotificationSettings}
        />

        <Route
          path="/settings/metadata"
          component={MetadataSettings}
        />

        <Route
          path="/settings/tags"
          component={TagSettings}
        />

        <Route
          path="/settings/general"
          component={GeneralSettingsConnector}
        />

        <Route
          path="/settings/ui"
          component={UISettingsConnector}
        />

        <Route
          path="/settings/development"
          component={DevelopmentSettingsConnector}
        />

        {/*
        System
      */}

        <Route
          path="/system/status"
          component={Status}
        />

        <Route
          path="/system/tasks"
          component={Tasks}
        />

        <Route
          path="/system/backup"
          component={BackupsConnector}
        />

        <Route
          path="/system/updates"
          component={Updates}
        />

        <Route
          path="/system/events"
          component={LogsTableConnector}
        />

        <Route
          path="/system/logs/files"
          component={Logs}
        />

        {/*
        Not Found
      */}

        <Route
          path="*"
          component={NotFound}
        />

      </Switch>
    </Suspense>
  );
}

AppRoutes.propTypes = {
  app: PropTypes.func.isRequired
};

export default AppRoutes;
