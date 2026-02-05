import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

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
  const requestAnimationFrameRef = useRef();

  useEffect(() => {
    const progressStep = () => {
      requestAnimationFrameRef.current = window.requestAnimationFrame(() => {
        setProgress((currentProgress) => {
          if (currentProgress < targetProgress) {
            const nextProgress = currentProgress + 1;
            // Continue the animation if we are not at the target
            if (nextProgress < targetProgress) {
              progressStep();
            }
            return nextProgress;
          }
          return targetProgress;
        });
      });
    };

    // Start the animation
    progressStep();

    return () => {
      // Cleanup: cancel the animation frame on unmount or if target changes
      if (requestAnimationFrameRef.current) {
        window.cancelAnimationFrame(requestAnimationFrameRef.current);
      }
    };
  }, [targetProgress]);

  // ⚡ Bolt: Memoize SVG geometry calculations.
  // These values only change when size or strokeWidth props change,
  // so we prevent recalculating them on every animation frame.
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

export default CircularProgressBar;
