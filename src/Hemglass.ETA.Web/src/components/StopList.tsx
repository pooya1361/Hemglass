import type { EtaResult } from '../types/eta';
import './StopList.css';

interface StopListProps {
  etaResult: EtaResult | null;
  loading: boolean;
  error: string | null;
}

function formatTime(isoString: string): string {
  const date = new Date(isoString);
  return date.toLocaleTimeString('sv-SE', { hour: '2-digit', minute: '2-digit' });
}

export function StopList({ etaResult, loading, error }: StopListProps) {
  if (loading) {
    return <div className="stop-list loading">Loading...</div>;
  }

  if (!etaResult) {
    return (
      <div className="stop-list empty">
        {error ? (
          <p className="error-message">{error}</p>
        ) : (
          <p>Enter a Stop ID and click Fetch to load route.</p>
        )}
      </div>
    );
  }

  return (
    <div className="stop-list">
      <div className="stop-list-header">
        <div className="current-stop">
          <span className="label">Truck at:</span>
          <span className="value">{etaResult.currentStopAddress}</span>
        </div>
        <div className="remaining">
          <span className="value">{etaResult.remainingStopsCount}</span>
          <span className="label"> stops remaining</span>
        </div>
      </div>

      <div className="stops">
        {etaResult.stops.map((stop, index) => (
          <div key={stop.stopId} className="stop-item">
            <div className="stop-number">{index + 1}</div>
            <div className="stop-details">
              <div className="stop-name">{stop.name}</div>
              <div className="stop-eta">
                <span className="time">{formatTime(stop.estimatedArrival)}</span>
                <span className="minutes">in {stop.minutesFromNow} min</span>
                <span className="travel">({stop.travelMinutes} min drive)</span>
              </div>
            </div>
            {index < etaResult.stops.length - 1 && (
              <div className="travel-time">
                + {etaResult.averageDwellMinutes} min dwell
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="stop-list-footer">
        <span className="label">Avg. dwell time:</span>
        <span className="value">{etaResult.averageDwellMinutes} min</span>
      </div>
    </div>
  );
}
