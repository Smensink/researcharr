import PropTypes from 'prop-types';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import styles from './CircularProgressBar.css';

// ⚡ Bolt: Refactored to a functional component with hooks.
// This is the first step towards optimization. The next step will be to memoize calculations.
const CircularProgressBar = (props) => {
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
  const animationFrameIdRef = useRef();

  useEffect(() => {
    let id = null;

    const step = () => {
      setProgress((p) => {
        if (p < targetProgress) {
          id = window.requestAnimationFrame(step);
          return p + 1;
        }

        if (p > targetProgress) {
          id = window.requestAnimationFrame(step);
          return p - 1;
        }

        return p;
      });
    };

    id = window.requestAnimationFrame(step);

    animationFrameIdRef.current = id;

    return () => {
      window.cancelAnimationFrame(animationFrameIdRef.current);
    };
  }, [targetProgress]);

  // ⚡ Bolt: Memoize expensive calculations.
  // These values are now only recalculated when size or strokeWidth change,
  // not on every single animation frame.
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

// ⚡ Bolt: Wrap with React.memo.
// This prevents the component from re-rendering if its props are unchanged.
export default React.memo(CircularProgressBar);
