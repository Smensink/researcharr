import PropTypes from 'prop-types';
import React, { useEffect, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Converted to a functional component for performance and readability.
const CircularProgressBar = ({
  className,
  containerClassName,
  size,
  strokeWidth,
  strokeColor,
  showProgressText,
  progress: targetProgress
}) => {
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    let animationFrameId = null;

    const step = () => {
      setProgress((currentProgress) => {
        // Animate progress upwards until it reaches the target
        if (currentProgress < targetProgress) {
          animationFrameId = window.requestAnimationFrame(step);
          return currentProgress + 1;
        }
        // If progress is at or beyond the target, stop the animation
        return targetProgress > currentProgress ? targetProgress : currentProgress;
      });
    };

    // Start the animation
    animationFrameId = window.requestAnimationFrame(step);

    // Cleanup function to cancel the animation frame when the component unmounts or the targetProgress changes
    return () => {
      window.cancelAnimationFrame(animationFrameId);
    };
  }, [targetProgress]); // Rerun the effect only when the target progress changes

  const center = size / 2;
  const radius = center - strokeWidth;
  const circumference = Math.PI * (radius * 2);
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

export default React.memo(CircularProgressBar);
