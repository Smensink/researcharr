import PropTypes from 'prop-types';
import React, { memo, useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: This component is wrapped in React.memo to prevent unnecessary re-renders
// if its props have not changed. This is a performance optimization.
const CircularProgressBar = memo((props) => {
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

  useEffect(() => {
    // By resetting progress to 0, we ensure the animation
    // correctly starts from the beginning each time the target changes.
    // This fixes a bug where a decreasing target would stall the animation.
    setProgress(0);

    const progressStep = () => {
      setProgress((prevProgress) => {
        if (prevProgress >= targetProgress) {
          return targetProgress; // Stop at the target
        }
        const newProgress = prevProgress + 1;
        if (newProgress <= targetProgress) {
          requestAnimationFrameRef.current = window.requestAnimationFrame(progressStep);
        }
        return newProgress;
      });
    };

    // Only start the animation if there's progress to be made.
    if (targetProgress > 0) {
      requestAnimationFrameRef.current = window.requestAnimationFrame(progressStep);
    }

    return () => {
      if (requestAnimationFrameRef.current) {
        window.cancelAnimationFrame(requestAnimationFrameRef.current);
      }
    };
  }, [targetProgress]);

  // ⚡ Bolt: Memoize calculated values to prevent recalculating on every animation frame.
  // These values only change when size or strokeWidth props change.
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

CircularProgressBar.defaultProps = {
  className: styles.circularProgressBar,
  containerClassName: styles.circularProgressBarContainer,
  size: 60,
  strokeWidth: 5,
  strokeColor: '#00A65B',
  showProgressText: false
};

CircularProgressBar.displayName = 'CircularProgressBar';

export default CircularProgressBar;
