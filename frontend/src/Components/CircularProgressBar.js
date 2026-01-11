import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: This component is wrapped in React.memo to prevent unnecessary re-renders.
const CircularProgressBar = (props) => {
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    setProgress(0);
  }, [props.progress]);

  useEffect(() => {
    let requestAnimationFrameId = null;

    const progressStep = () => {
      setProgress((prevProgress) => {
        if (prevProgress < props.progress) {
          requestAnimationFrameId = window.requestAnimationFrame(progressStep);
          return prevProgress + 1;
        }

        return prevProgress;
      });
    };

    requestAnimationFrameId = window.requestAnimationFrame(progressStep);

    return () => {
      if (requestAnimationFrameId) {
        window.cancelAnimationFrame(requestAnimationFrameId);
      }
    };
  }, [props.progress]);

  const {
    className,
    containerClassName,
    size,
    strokeWidth,
    strokeColor,
    showProgressText
  } = props;

  // ⚡ Bolt: useMemo caches the following calculations, preventing them from re-running on every animation frame.
  // This is a performance optimization that makes the animation smoother.
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

export default React.memo(CircularProgressBar);
