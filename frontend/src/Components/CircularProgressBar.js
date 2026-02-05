import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

const CircularProgressBar = (props) => {
  const {
    className,
    containerClassName,
    size,
    progress: targetProgress,
    strokeWidth,
    strokeColor,
    showProgressText
  } = props;

  const [progress, setProgress] = useState(0);
  const requestRef = useRef();

  useEffect(() => {
    const step = () => {
      setProgress((p) => {
        if (p < targetProgress) {
          requestRef.current = window.requestAnimationFrame(step);
          return p + 1;
        }

        return p;
      });
    };

    requestRef.current = window.requestAnimationFrame(step);

    return () => {
      if (requestRef.current) {
        window.cancelAnimationFrame(requestRef.current);
      }
    };
  }, [targetProgress]);

  const {
    center,
    radius,
    circumference
  } = useMemo(() => {
    // ⚡ Bolt: Memoize geometric calculations to prevent re-computation on every animation frame.
    // These values only depend on `size` and `strokeWidth`, which rarely change.
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
