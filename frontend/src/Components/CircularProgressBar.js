import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

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
  const animationFrameId = useRef(null);

  useEffect(() => {
    const progressStep = () => {
      animationFrameId.current = window.requestAnimationFrame(() => {
        setProgress((prev) => {
          if (prev < targetProgress) {
            progressStep();
            return prev + 1;
          }
          return targetProgress;
        });
      });
    };

    progressStep();

    return () => {
      if (animationFrameId.current) {
        window.cancelAnimationFrame(animationFrameId.current);
      }
    };
  }, [targetProgress]);

  const { center, radius, circumference, sizeInPixels } = useMemo(() => {
    // ⚡ Bolt: Memoize calculated values to avoid re-computing on every animation frame.
    // These values only change when size or strokeWidth props change.
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);
    const sizeInPixelsVal = `${size}px`;
    return { center: centerVal, radius: radiusVal, circumference: circumferenceVal, sizeInPixels: sizeInPixelsVal };
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
        <div className={styles.circularProgressBarText}>
          {progressText}
        </div>
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
