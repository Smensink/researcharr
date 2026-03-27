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
  progress: targetProgress // Renamed to avoid conflict with state variable
}) => {
  const [progress, setProgress] = useState(0);
  const animationFrameRef = useRef(null);

  useEffect(() => {
    // This effect manages the animation loop.
    // It's designed to replicate the behavior of the original class component's
    // componentDidMount and componentDidUpdate lifecycle methods.

    const step = () => {
      animationFrameRef.current = window.requestAnimationFrame(() => {
        setProgress((prevProgress) => {
          // If we haven't reached the target, schedule the next frame and increment.
          if (prevProgress < targetProgress) {
            step();
            return prevProgress + 1;
          }

          // If the target is met or exceeded, stop the animation.
          // This also handles cases where the targetProgress is lowered.
          return prevProgress;
        });
      });
    };

    // Start the animation.
    step();

    // The cleanup function is critical. It runs when the component unmounts,
    // or when the `targetProgress` dependency changes. This prevents memory leaks
    // and cancels the old animation before starting a new one.
    return () => {
      if (animationFrameRef.current) {
        window.cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [targetProgress]); // The effect re-runs whenever the target progress changes.

  // ⚡ Bolt: Memoize SVG geometry calculations.
  // These values only depend on size and strokeWidth, which rarely change.
  // By using useMemo, we avoid re-calculating them on every single animation frame,
  // making the component more efficient.
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

      {showProgressText &&
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
