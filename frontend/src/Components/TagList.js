import PropTypes from 'prop-types';
import React, { memo, useMemo } from 'react';
import { kinds } from 'Helpers/Props';
import Label from './Label';
import styles from './TagList.css';

function TagList({ tags, tagList }) {
  // ⚡ Bolt: Memoize the sorted tags calculation to avoid expensive O(N*M) lookups and O(K log K) sorts on every render.
  const sortedTags = useMemo(() => {
    return tags
      .map((tagId) => tagList.find((tag) => tag.id === tagId))
      .filter((tag) => !!tag)
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [tags, tagList]);

  return (
    <div className={styles.tags}>
      {
        sortedTags.map((tag) => {
          return (
            <Label
              key={tag.id}
              kind={kinds.INFO}
            >
              {tag.label}
            </Label>
          );
        })
      }
    </div>
  );
}

TagList.propTypes = {
  tags: PropTypes.arrayOf(PropTypes.number).isRequired,
  tagList: PropTypes.arrayOf(PropTypes.object).isRequired
};

export default memo(TagList);
