import { Icon, LatLngBounds } from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useEffect } from 'react';
import { CircleMarker, MapContainer, Marker, Polyline, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import type { StopEta, StopMarker, TruckPosition } from '../types/eta';

// Fix for default marker icons in React-Leaflet
const truckIcon = new Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png',
  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41]
});

const etaStopIcon = new Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png',
  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
  iconSize: [20, 33],
  iconAnchor: [10, 33],
  popupAnchor: [1, -28],
  shadowSize: [33, 33]
});

interface TruckMapProps {
  truckPosition: TruckPosition | null;
  etaStops: StopEta[];      // Stops with ETA (up to 10)
  allStops: StopMarker[];   // All stops on route (60-70)
  onMapClick: (position: TruckPosition) => void;
  onStopClick: (stopId: number, position: TruckPosition) => void;
}

function MapClickHandler({ onMapClick }: { onMapClick: (position: TruckPosition) => void }) {
  useMapEvents({
    click: (e) => {
      onMapClick({ lat: e.latlng.lat, lng: e.latlng.lng });
    },
  });
  return null;
}

function FitBounds({ allStops }: { allStops: StopMarker[] }) {
  const map = useMap();

  useEffect(() => {
    if (allStops.length === 0) return;

    const points: [number, number][] = allStops.map(stop => [stop.latitude, stop.longitude]);

    if (points.length >= 1) {
      const bounds = new LatLngBounds(points);
      map.fitBounds(bounds, { padding: [30, 30] });
    }
  }, [allStops, map]);

  return null;
}

export function TruckMap({ truckPosition, etaStops, allStops, onMapClick, onStopClick }: TruckMapProps) {
  // Default center: Stockholm
  const defaultCenter: [number, number] = [59.33, 18.07];

  // Build route line through ALL stops
  const routePoints: [number, number][] = allStops.map(stop => [stop.latitude, stop.longitude]);

  // Set of stopIds that have ETA (to highlight them differently)
  const etaStopIds = new Set(etaStops.map(s => s.stopId));

  return (
    <MapContainer
      center={defaultCenter}
      zoom={13}
      style={{ height: '100%', width: '100%' }}
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/">CARTO</a>'
        url="https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png"
      />
      <MapClickHandler onMapClick={onMapClick} />
      <FitBounds allStops={allStops} />

      {/* Route line through all stops */}
      {routePoints.length >= 2 && (
        <Polyline
          positions={routePoints}
          color="#90caf9"
          weight={3}
          opacity={0.8}
        />
      )}

      {/* All stops as small circles - clickable to set truck position */}
      {allStops.map((stop) => {
        const isEtaStop = etaStopIds.has(stop.stopId);
        if (isEtaStop) return null; // Skip, will render with marker below

        return (
          <CircleMarker
            key={stop.stopId}
            center={[stop.latitude, stop.longitude]}
            radius={8}
            fillColor="#1976d2"
            fillOpacity={0.7}
            color="#1565c0"
            weight={2}
            eventHandlers={{
              click: (e) => {
                e.originalEvent.stopPropagation();
                onStopClick(stop.stopId, { lat: stop.latitude, lng: stop.longitude });
              }
            }}
          />
        );
      })}

      {/* ETA stops as larger markers - clickable to set truck position */}
      {etaStops.map((stop) => (
        <Marker
          key={stop.stopId}
          position={[stop.latitude, stop.longitude]}
          icon={etaStopIcon}
          eventHandlers={{
            click: (e) => {
              e.originalEvent.stopPropagation();
              onStopClick(stop.stopId, { lat: stop.latitude, lng: stop.longitude });
            }
          }}
        />
      ))}

      {/* Truck marker */}
      {truckPosition && (
        <Marker position={[truckPosition.lat, truckPosition.lng]} icon={truckIcon} />
      )}
    </MapContainer>
  );
}
