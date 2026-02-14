import PropTypes from 'prop-types';
import React, { memo, useEffect, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

const CircularProgressBar = (props) => {
  const {
    className,
    containerClassName,
    size,
    progress: targetProgress,
    strokeWidth,
    strokeColor,
    showProgressText
  } = props;

  const [progress, setProgress] = useState(0);
  const requestRef = useRef();

  useEffect(() => {
    // Animate from current progress to target progress
    const animate = () => {
      setProgress((currentProgress) => {
        if (currentProgress < targetProgress) {
          requestRef.current = requestAnimationFrame(animate);
          return currentProgress + 1;
        }

        // If current progress is greater than target, snap to target
        if (currentProgress > targetProgress) {
          return targetProgress;
        }

        // Otherwise, we're at the target. Stop animating.
        return currentProgress;
      });
    };

    // Cancel any existing animation frame before starting a new one.
    // This is crucial for when the targetProgress prop changes.
    if (requestRef.current) {
      cancelAnimationFrame(requestRef.current);
    }

    // Start the animation
    requestRef.current = requestAnimationFrame(animate);

    // Cleanup function to cancel animation on component unmount
    return () => {
      if (requestRef.current) {
        cancelAnimationFrame(requestRef.current);
      }
    };
  }, [targetProgress]); // Rerun this effect only when targetProgress changes

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

export default memo(CircularProgressBar);
