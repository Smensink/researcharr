import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component with hooks.
// `useMemo` is used to cache expensive geometric calculations,
// preventing them from being re-run on every animation frame.
// `React.memo` prevents re-renders if props haven't changed.
const CircularProgressBar = React.memo(({
  className,
  containerClassName,
  size,
  strokeWidth,
  strokeColor,
  showProgressText,
  progress: targetProgress
}) => {
  const [progress, setProgress] = useState(0);
  const requestRef = useRef();

  // Memoize geometric calculations
  const { center, radius, circumference } = useMemo(() => {
    // ⚡ Bolt: These values are now calculated only when size or strokeWidth change,
    // not on every single animation frame.
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);

    return { center: centerVal, radius: radiusVal, circumference: circumferenceVal };
  }, [size, strokeWidth]);

  // Animation effect
  useEffect(() => {
    // ⚡ Bolt: Improved animation logic to handle both increasing and
    // decreasing progress values, making the component more robust.
    const step = () => {
      setProgress((prevProgress) => {
        if (prevProgress < targetProgress) {
          // Animate upwards
          requestRef.current = window.requestAnimationFrame(step);

          return prevProgress + 1;
        }

        if (prevProgress > targetProgress) {
          // Animate downwards
          requestRef.current = window.requestAnimationFrame(step);

          return prevProgress - 1;
        }

        // Stop animation
        return prevProgress;
      });
    };

    // Start the animation
    requestRef.current = window.requestAnimationFrame(step);

    // Cleanup: cancel animation frame on unmount or if targetProgress changes
    return () => {
      if (requestRef.current) {
        window.cancelAnimationFrame(requestRef.current);
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

      {showProgressText && (
        <div className={styles.circularProgressBarText}>
          {progressText}
        </div>
      )}
    </div>
  );
});

CircularProgressBar.displayName = 'CircularProgressBar';

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
