// ⚡ Bolt: This component was refactored to a functional component and optimized with useMemo.
// The geometric calculations (center, radius, circumference) are now memoized,
// preventing them from being re-calculated on every animation frame.
// This reduces redundant work and makes the animation more efficient.

import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

const CircularProgressBar = (props) => {
  const {
    className,
    containerClassName,
    size,
    strokeWidth,
    strokeColor,
    showProgressText,
    progress: targetProgress
  } = props;

  const [progress, setProgress] = useState(0);
  const animationFrameId = useRef();

  // ⚡ Bolt: Memoize geometric calculations to avoid re-computing on every animation frame.
  const { center, radius, circumference } = useMemo(() => {
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);

    return { center: centerVal, radius: radiusVal, circumference: circumferenceVal };
  }, [size, strokeWidth]);

  useEffect(() => {
    const animate = () => {
      animationFrameId.current = requestAnimationFrame(() => {
        setProgress((p) => {
          if (p < targetProgress) {
            animate();

            return p + 1;
          }

          return p;
        });
      });
    };

    // Resetting progress to 0 on target change makes the animation predictable.
    // The old implementation had a bug where lowering the target progress
    // would not update the UI.
    setProgress(0);

    cancelAnimationFrame(animationFrameId.current);
    animate();

    return () => {
      if (animationFrameId.current) {
        window.cancelAnimationFrame(animationFrameId.current);
      }
    };
  }, [targetProgress]);

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

export default React.memo(CircularProgressBar);
