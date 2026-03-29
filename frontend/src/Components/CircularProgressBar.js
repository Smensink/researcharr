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
    progress: targetProgress // Rename to avoid conflict with state variable
  } = props;

  const [progress, setProgress] = useState(0);

  // Animate the progress bar to the target value
  useEffect(() => {
    let animationFrameId = null;

    const step = () => {
      animationFrameId = window.requestAnimationFrame(() => {
        setProgress((currentProgress) => {
          if (currentProgress < targetProgress) {
            step(); // Continue animation
            return currentProgress + 1;
          }
          return targetProgress; // Clamp to the final value
        });
      });
    };

    step(); // Start the animation

    return () => {
      if (animationFrameId) {
        window.cancelAnimationFrame(animationFrameId);
      }
    };
  }, [targetProgress]);

  // ⚡ Bolt: Memoizing these geometric calculations prevents them from being re-run on every single animation frame.
  // They are only recalculated when the size or strokeWidth props change.
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
