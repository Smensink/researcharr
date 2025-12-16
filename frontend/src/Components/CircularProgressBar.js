import PropTypes from 'prop-types';
import React, { useEffect, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

const CircularProgressBar = (props) => {
  const [currentProgress, setCurrentProgress] = useState(0);
  const requestAnimationFrameRef = useRef(null);

  const {
    className,
    containerClassName,
    size,
    strokeWidth,
    strokeColor,
    showProgressText,
    progress: targetProgress
  } = props;

  useEffect(() => {
    const progressStep = () => {
      requestAnimationFrameRef.current = window.requestAnimationFrame(() => {
        setCurrentProgress((prevProgress) => {
          if (prevProgress < targetProgress) {
            progressStep();
            return prevProgress + 1;
          }
          return prevProgress;
        });
      });
    };

    progressStep();

    return () => {
      if (requestAnimationFrameRef.current) {
        window.cancelAnimationFrame(requestAnimationFrameRef.current);
      }
    };
  }, [targetProgress]);

  const center = size / 2;
  const radius = center - strokeWidth;
  const circumference = Math.PI * (radius * 2);
  const sizeInPixels = `${size}px`;
  const strokeDashoffset = ((100 - currentProgress) / 100) * circumference;
  const progressText = `${Math.round(currentProgress)}%`;

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

export default React.memo(CircularProgressBar);
