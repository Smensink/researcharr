import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component to enable modern React optimizations.
const CircularProgressBar = React.memo(({
  className = styles.circularProgressBar,
  containerClassName = styles.circularProgressBarContainer,
  size = 60,
  progress: targetProgress,
  strokeWidth = 5,
  strokeColor = '#00A65B',
  showProgressText = false
}) => {
  const [progress, setProgress] = useState(0);
  const animationFrameRef = useRef();

  useEffect(() => {
    const step = () => {
      setProgress((currentProgress) => {
        if (currentProgress < targetProgress) {
          animationFrameRef.current = window.requestAnimationFrame(step);
          return currentProgress + 1;
        }

        return currentProgress;
      });
    };

    animationFrameRef.current = window.requestAnimationFrame(step);

    return () => {
      if (animationFrameRef.current) {
        window.cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [targetProgress]);

  // ⚡ Bolt: Memoize expensive calculations to prevent re-computing on every animation frame.
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
});

CircularProgressBar.propTypes = {
  className: PropTypes.string,
  containerClassName: PropTypes.string,
  size: PropTypes.number,
  progress: PropTypes.number.isRequired,
  strokeWidth: PropTypes.number,
  strokeColor: PropTypes.string,
  showProgressText: PropTypes.bool
};

export default CircularProgressBar;
