import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component with memoization.
// The original class component recalculated SVG properties (center, radius, circumference)
// on every single animation frame. This is inefficient as these values only depend on props
// (`size`, `strokeWidth`) that rarely change.
//
// By converting to a functional component, we can use the `useMemo` hook to cache these
// calculations. Now, they are only re-computed when the relevant props change, not during
// every step of the animation loop. This prevents dozens of unnecessary calculations per
// second, leading to a smoother animation and more efficient rendering.
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

  const { center, radius, circumference } = useMemo(() => {
    const centerVal = size / 2;
    const radiusVal = centerVal - strokeWidth;
    const circumferenceVal = Math.PI * (radiusVal * 2);

    return { center: centerVal, radius: radiusVal, circumference: circumferenceVal };
  }, [size, strokeWidth]);

  useEffect(() => {
    // This effect runs the animation loop.
    // It is re-triggered whenever `targetProgress` changes, which cancels the
    // old loop and starts a new one, preserving the current `progress` state.
    const step = () => {
      setProgress((currentProgress) => {
        if (currentProgress < targetProgress) {
          // If we haven't reached the target, schedule the next frame.
          requestAnimationFrameRef.current = window.requestAnimationFrame(step);
          return currentProgress + 1;
        }

        // Otherwise, stop the animation.
        return currentProgress;
      });
    };

    requestAnimationFrameRef.current = window.requestAnimationFrame(step);

    // The cleanup function is crucial. It cancels the animation frame when the
    // component unmounts or when `targetProgress` changes, preventing memory leaks
    // and multiple conflicting animation loops.
    return () => {
      if (requestAnimationFrameRef.current) {
        window.cancelAnimationFrame(requestAnimationFrameRef.current);
      }
    };
  }, [targetProgress]);

  const strokeDashoffset = ((100 - progress) / 100) * circumference;
  const progressText = `${Math.round(progress)}%`;
  const sizeInPixels = `${size}px`;

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
