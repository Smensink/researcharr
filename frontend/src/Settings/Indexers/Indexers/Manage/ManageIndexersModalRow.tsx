import React, { useCallback } from 'react';
import Label from 'Components/Label';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import TagListConnector from 'Components/TagListConnector';
import { kinds } from 'Helpers/Props';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import styles from './ManageIndexersModalRow.css';

interface ManageIndexersModalRowProps {
  id: number;
  name: string;
  enableRss: boolean;
  enableAutomaticSearch: boolean;
  enableInteractiveSearch: boolean;
  priority: number;
  implementation: string;
  tags: number[];
  statistics?: any;
  columns: Column[];
  isSelected?: boolean;
  onSelectedChange(result: SelectStateInputProps): void;
}

function ManageIndexersModalRow(props: ManageIndexersModalRowProps) {
  const {
    id,
    isSelected,
    name,
    enableRss,
    enableAutomaticSearch,
    enableInteractiveSearch,
    priority,
    implementation,
    tags,
    statistics,
    onSelectedChange,
  } = props;

  const onSelectedChangeWrapper = useCallback(
    (result: SelectStateInputProps) => {
      onSelectedChange({
        ...result,
      });
    },
    [onSelectedChange]
  );

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={onSelectedChangeWrapper}
      />

      <TableRowCell className={styles.name}>{name}</TableRowCell>

      <TableRowCell className={styles.implementation}>
        {implementation}
      </TableRowCell>

      <TableRowCell className={styles.enableRss}>
        <Label
          kind={enableRss ? kinds.SUCCESS : kinds.DISABLED}
          outline={!enableRss}
        >
          {enableRss ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className={styles.enableAutomaticSearch}>
        <Label
          kind={enableAutomaticSearch ? kinds.SUCCESS : kinds.DISABLED}
          outline={!enableAutomaticSearch}
        >
          {enableAutomaticSearch ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className={styles.enableInteractiveSearch}>
        <Label
          kind={enableInteractiveSearch ? kinds.SUCCESS : kinds.DISABLED}
          outline={!enableInteractiveSearch}
        >
          {enableInteractiveSearch ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className={styles.priority}>{priority}</TableRowCell>

      <TableRowCell className={styles.statistics}>
        {statistics ? (
          <div>
            {statistics.recentFailures > 0 && (
              <Label
                kind={kinds.WARNING}
                title={translate('RecentFailures', { count: statistics.recentFailures })}
              >
                {statistics.recentFailures} {translate('Failures')}
              </Label>
            )}
            {statistics.totalFailures > 0 && statistics.recentFailures === 0 && (
              <Label
                kind={kinds.INFO}
                title={translate('TotalFailures', { count: statistics.totalFailures })}
              >
                {statistics.totalFailures} {translate('Total')}
              </Label>
            )}
            {statistics.isHealthy === false && (
              <Label
                kind={kinds.DANGER}
                title={translate('IndexerUnhealthy')}
              >
                {translate('Unhealthy')}
              </Label>
            )}
            {statistics.isHealthy === true && statistics.recentFailures === 0 && statistics.totalFailures === 0 && (
              <Label
                kind={kinds.SUCCESS}
                title={translate('IndexerHealthy')}
              >
                {translate('Healthy')}
              </Label>
            )}
            {!statistics.recentFailures && !statistics.totalFailures && statistics.isHealthy !== false && (
              <span>-</span>
            )}
          </div>
        ) : (
          <span>-</span>
        )}
      </TableRowCell>

      <TableRowCell className={styles.tags}>
        <TagListConnector tags={tags} />
      </TableRowCell>
    </TableRow>
  );
}

export default ManageIndexersModalRow;
