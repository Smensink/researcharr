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
  const requestAnimationFrameRef = useRef();

  // ⚡ Bolt: Memoize expensive calculations.
  // These values only change when size or strokeWidth props change,
  // so we avoid recalculating them on every single animation frame.
  const { center, radius, circumference } = useMemo(() => {
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);

    return {
      center: centerVal,
      radius: radiusVal,
      circumference: circumferenceVal
    };
  }, [size, strokeWidth]);

  useEffect(() => {
    const progressStep = () => {
      setProgress((prevProgress) => {
        const nextProgress = prevProgress + 1;
        if (nextProgress < targetProgress) {
          requestAnimationFrameRef.current = window.requestAnimationFrame(progressStep);
        }

        return nextProgress;
      });
    };

    // Reset animation when targetProgress changes
    setProgress(0);
    if (requestAnimationFrameRef.current) {
      window.cancelAnimationFrame(requestAnimationFrameRef.current);
    }
    requestAnimationFrameRef.current = window.requestAnimationFrame(progressStep);

    return () => {
      if (requestAnimationFrameRef.current) {
        window.cancelAnimationFrame(requestAnimationFrameRef.current);
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

export default CircularProgressBar;
