import PropTypes from 'prop-types';
import React, { memo, useEffect, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// This component is wrapped in React.memo to prevent unnecessary re-renders.
// Since it's a purely presentational component, it will only re-render when its props change,
// improving performance in lists or frequently updated UIs.
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

  const [animatedProgress, setAnimatedProgress] = useState(0);
  const requestRef = useRef();

  useEffect(() => {
    // Reset animation when target progress changes, mimicking the old lifecycle.
    setAnimatedProgress(0);
  }, [targetProgress]);

  useEffect(() => {
    const step = () => {
      if (animatedProgress < targetProgress) {
        setAnimatedProgress((prev) => prev + 1);
      }
    };

    if (animatedProgress < targetProgress) {
      requestRef.current = requestAnimationFrame(step);
    }

    // Cleanup function to cancel the animation frame on unmount or re-render.
    return () => {
      if (requestRef.current) {
        cancelAnimationFrame(requestRef.current);
      }
    };
  }, [animatedProgress, targetProgress]);

  const center = size / 2;
  const radius = center - strokeWidth;
  const circumference = Math.PI * (radius * 2);
  const sizeInPixels = `${size}px`;
  const strokeDashoffset = ((100 - animatedProgress) / 100) * circumference;
  const progressText = `${Math.round(animatedProgress)}%`;

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

export default CircularProgressBar;
