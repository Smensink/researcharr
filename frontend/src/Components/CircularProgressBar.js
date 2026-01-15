import PropTypes from 'prop-types';
import React, { memo, useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component to enable optimization with hooks.
const CircularProgressBar = (props) => {
  const {
    className,
    containerClassName,
    size,
    strokeWidth,
    strokeColor,
    showProgressText,
    progress: targetProgress // Renaming for clarity inside the component
  } = props;

  const [progress, setProgress] = useState(0);
  const animationFrameRef = useRef();

  useEffect(() => {
    // This function represents one step of the animation.
    const step = () => {
      setProgress((currentProgress) => {
        if (currentProgress < targetProgress) {
          // If we haven't reached the target, schedule the next step.
          animationFrameRef.current = requestAnimationFrame(step);

          return currentProgress + 1;
        }
        // We've reached the target, so we stop.
        return currentProgress;
      });
    };

    // The original class component would cancel and restart the animation
    // on prop changes, continuing from the current state. This effect
    // replicates that behavior.
    cancelAnimationFrame(animationFrameRef.current);
    animationFrameRef.current = requestAnimationFrame(step);

    // Cleanup function is called when the component unmounts
    // or when the effect re-runs.
    return () => {
      cancelAnimationFrame(animationFrameRef.current);
    };
  }, [targetProgress]);

  // ⚡ Bolt: Memoize SVG calculations to prevent re-computing on every animation frame.
  // These values only change when size or strokeWidth props change.
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

// ⚡ Bolt: Wrap with React.memo to prevent re-renders when props are unchanged.
export default memo(CircularProgressBar);
