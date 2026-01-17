import { useState } from 'react';
import { TruckMap } from './components/TruckMap';
import { StopList } from './components/StopList';
import type { EtaResult, TruckPosition, RouteInfo, StopMarker } from './types/eta';
import './App.css';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5255';

function App() {
  const [stopId, setStopId] = useState<string>('');
  const [truckPosition, setTruckPosition] = useState<TruckPosition | null>(null);
  const [routeInfo, setRouteInfo] = useState<RouteInfo | null>(null);
  const [etaResult, setEtaResult] = useState<EtaResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch route info (no ETA calculation)
  const fetchRoute = async (stopIdToFetch: string) => {
    if (!stopIdToFetch) return;

    setLoading(true);
    setError(null);

    try {
      const response = await fetch(`${API_BASE_URL}/api/route/${stopIdToFetch}`);

      if (!response.ok) {
        if (response.status === 404) {
          throw new Error('Route not found');
        }
        throw new Error('Failed to fetch route');
      }

      const data: RouteInfo = await response.json();
      setRouteInfo(data);
      setEtaResult(null); // Clear ETAs until truck position is set
    } catch (err) {
      console.error('Fetch error:', err);
      const message = err instanceof Error ? err.message : 'Failed to fetch route';
      if (message.includes('Failed to fetch') || message.includes('NetworkError')) {
        setError('Cannot connect to API');
      } else {
        setError(message);
      }
      setRouteInfo(null);
    } finally {
      setLoading(false);
    }
  };

  // Fetch ETA with truck position
  const fetchEta = async (stopIdToFetch: string, position: TruckPosition, fromStopId?: number) => {
    if (!stopIdToFetch || !position) return;

    setLoading(true);
    setError(null);

    try {
      let url = `${API_BASE_URL}/api/eta/${stopIdToFetch}?lat=${position.lat}&lon=${position.lng}`;
      if (fromStopId) {
        url += `&fromStopId=${fromStopId}`;
      }
      const response = await fetch(url);

      if (!response.ok) {
        if (response.status === 404) {
          throw new Error('Route not found');
        }
        throw new Error('Failed to fetch ETA');
      }

      const data: EtaResult = await response.json();
      setEtaResult(data);
    } catch (err) {
      console.error('Fetch error:', err);
      const message = err instanceof Error ? err.message : 'Failed to fetch ETA';
      if (message.includes('Failed to fetch') || message.includes('NetworkError')) {
        setError('Cannot connect to API');
      } else {
        setError(message);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleFetch = () => {
    if (!stopId) {
      setError('Enter a Stop ID');
      return;
    }
    fetchRoute(stopId);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleFetch();
    }
  };

  const handleMapClick = (position: TruckPosition) => {
    // Find nearest stop on the route
    const stops = routeInfo?.stops ?? [];
    if (stops.length === 0) {
      // No route loaded yet, just set position
      setTruckPosition(position);
      setError(null);
      return;
    }

    // Calculate distance to each stop and find nearest
    let nearestStop = stops[0];
    let minDist = Infinity;
    for (const stop of stops) {
      const dist = Math.pow(stop.latitude - position.lat, 2) + Math.pow(stop.longitude - position.lng, 2);
      if (dist < minDist) {
        minDist = dist;
        nearestStop = stop;
      }
    }

    // Set truck at nearest stop and fetch ETA from there
    setTruckPosition({ lat: nearestStop.latitude, lng: nearestStop.longitude });
    setError(null);
    if (stopId) {
      fetchEta(stopId, { lat: nearestStop.latitude, lng: nearestStop.longitude }, nearestStop.stopId);
    }
  };

  // Called from map stop markers - sets truck AT the stop and recalculates ETA
  const handleMapStopClick = (clickedStopId: number, position: TruckPosition) => {
    setTruckPosition(position);
    setError(null);
    // Keep the same stopId for route, but pass fromStopId to show stops after clicked one
    if (stopId) {
      fetchEta(stopId, position, clickedStopId);
    }
  };

  // Get stops for map display from route info
  const allStops: StopMarker[] = routeInfo?.stops ?? [];

  return (
    <div className="app">
      <header className="app-header">
        <h1>Hemglass ETA</h1>
        <div className="controls">
          <input
            type="text"
            placeholder="Stop ID"
            value={stopId}
            onChange={(e) => setStopId(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={loading}
            autoComplete="on"
            name="stopId"
          />
          <button onClick={handleFetch} disabled={loading}>
            {loading ? 'Loading...' : 'Fetch'}
          </button>
        </div>
        {truckPosition && (
          <div className="position-info">
            Truck: {truckPosition.lat.toFixed(5)}, {truckPosition.lng.toFixed(5)}
          </div>
        )}
        {routeInfo && !truckPosition && (
          <div className="position-hint">
            Click map to set truck position for ETA
          </div>
        )}
        {error && <div className="header-error">{error}</div>}
      </header>

      <main className="app-main">
        <div className="map-container">
          <TruckMap
            truckPosition={truckPosition}
            etaStops={etaResult?.stops ?? []}
            allStops={allStops}
            onMapClick={handleMapClick}
            onStopClick={handleMapStopClick}
          />
        </div>
        <aside className="sidebar">
          <StopList
            etaResult={etaResult}
            loading={loading}
            error={error}
          />
        </aside>
      </main>
    </div>
  );
}

export default App;
