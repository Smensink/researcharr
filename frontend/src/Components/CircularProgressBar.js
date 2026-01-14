import PropTypes from 'prop-types';
import React, { memo, useEffect, useMemo, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component to enable optimizations.
const CircularProgressBar = ({
  className,
  containerClassName,
  size,
  progress: targetProgress,
  strokeWidth,
  strokeColor,
  showProgressText
}) => {
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    // This effect handles the animation from the current progress to the target progress.
    let id = null;
    const animate = () => {
      setProgress((currentProgress) => {
        // Stop the animation if the progress has reached the target.
        if (currentProgress >= targetProgress) {
          return targetProgress;
        }

        // Otherwise, schedule the next frame.
        id = requestAnimationFrame(animate);
        return currentProgress + 1;
      });
    };

    // When the targetProgress prop changes, cancel any existing animation
    // and start a new one.
    cancelAnimationFrame(id);
    id = requestAnimationFrame(animate);

    // Cleanup: cancel the animation frame when the component unmounts or
    // the effect re-runs.
    return () => cancelAnimationFrame(id);
  }, [targetProgress]);

  // ⚡ Bolt: Memoize expensive calculations.
  // These values were recalculated on every animation frame in the original
  // implementation. useMemo ensures they are only re-computed when the
  // component's `size` or `strokeWidth` props change.
  const { center, radius, circumference } = useMemo(() => {
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);

    return { center: centerVal, radius: radiusVal, circumference: circumferenceVal };
  }, [size, strokeWidth]);

  const sizeInPixels = `${size}px`;
  const strokeDashoffset = ((100 - progress) / 100) * circumference;
  const progressText = `${Math.round(progress)}%`;

  return (
    <div
      className={containerClassName}
      style={{
        width: sizeInPixels,
        height: sizeInPixels,
        lineHeight: sizeInPixels
      }}
    >
      <svg
        className={className}
        version="1.1"
        xmlns="http://www.w3.org/2000/svg"
        width={size}
        height={size}
      >
        <circle
          fill="transparent"
          r={radius}
          cx={center}
          cy={center}
          strokeDasharray={circumference}
          style={{
            stroke: strokeColor,
            strokeWidth,
            strokeDashoffset
          }}
        />
      </svg>

      {
        showProgressText &&
          <div className={styles.circularProgressBarText}>
            {progressText}
          </div>
      }
    </div>
  );
};

CircularProgressBar.propTypes = {
  className: PropTypes.string,
  containerClassName: PropTypes.string,
  size: PropTypes.number,
  progress: PropTypes.number.isRequired,
  strokeWidth: PropTypes.number,
  strokeColor: PropTypes.string,
  showProgressText: PropTypes.bool
};

CircularProgressBar.defaultProps = {
  className: styles.circularProgressBar,
  containerClassName: styles.circularProgressBarContainer,
  size: 60,
  strokeWidth: 5,
  strokeColor: '#00A65B',
  showProgressText: false
};

export default memo(CircularProgressBar);
