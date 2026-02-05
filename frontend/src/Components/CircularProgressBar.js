import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component and memoized expensive calculations.
// This prevents recalculating geometry on every animation frame, making the animation smoother.
const CircularProgressBar = ({
  className,
  containerClassName,
  size,
  progress: targetProgress, // Renaming for clarity
  strokeWidth,
  strokeColor,
  showProgressText
}) => {
  const [progress, setProgress] = useState(0);
  const animationFrameId = useRef(null);

  // ⚡ Bolt: Memoize calculations to avoid re-computing on every single re-render,
  // which happens on each animation frame. These values only change if size or strokeWidth change.
  const { center, radius, circumference } = useMemo(() => {
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);

    return { center: centerVal, radius: radiusVal, circumference: circumferenceVal };
  }, [size, strokeWidth]);

  useEffect(() => {
    // This effect replicates the animation logic from the original class component.
    // It runs whenever the local `progress` state or the `targetProgress` prop changes.
    const cancelAnimation = () => {
      if (animationFrameId.current) {
        window.cancelAnimationFrame(animationFrameId.current);
      }
    };

    if (progress < targetProgress) {
      animationFrameId.current = window.requestAnimationFrame(() => {
        // Use the functional form of setState to ensure we have the latest value.
        setProgress((prevProgress) => prevProgress + 1);
      });
    }

    // Cleanup function to cancel the animation frame when the component unmounts
    // or when the dependencies (`progress`, `targetProgress`) change before the next effect runs.
    return cancelAnimation;
  }, [progress, targetProgress]);

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
        version='1.1'
        xmlns='http://www.w3.org/2000/svg'
        width={size}
        height={size}
      >
        <circle
          fill='transparent'
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

      {showProgressText &&
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

// ⚡ Bolt: Wrap with React.memo to prevent re-renders if props haven't changed.
export default React.memo(CircularProgressBar);
