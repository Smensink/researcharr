import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useState } from 'react';
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

  useEffect(() => {
    let animationFrameId = null;

    if (progress < targetProgress) {
      // If we're not at the target yet, schedule the next animation frame.
      animationFrameId = window.requestAnimationFrame(() => {
        setProgress(progress + 1);
      });
    }

    // If the targetProgress is lowered, the condition `progress < targetProgress`
    // will eventually be false, and the animation will stop, holding its value.
    // This matches the original component's behavior.

    return () => {
      // Cleanup function to cancel the frame when the component unmounts
      // or dependencies change before the frame has a chance to run.
      window.cancelAnimationFrame(animationFrameId);
    };
  }, [progress, targetProgress]);

  // ⚡ Bolt: Memoize calculations that only depend on props to avoid re-computing
  // them on every animation frame. This makes the animation more efficient.
  const { center, radius, circumference, sizeInPixels } = useMemo(() => {
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);
    const sizeInPixelsVal = `${size}px`;

    return {
      center: centerVal,
      radius: radiusVal,
      circumference: circumferenceVal,
      sizeInPixels: sizeInPixelsVal
    };
  }, [size, strokeWidth]);

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

      {showProgressText && (
        <div className={styles.circularProgressBarText}>{progressText}</div>
      )}
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

export default CircularProgressBar;
