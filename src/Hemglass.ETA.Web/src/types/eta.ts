export interface StopEta {
  stopId: number;
  name: string;
  latitude: number;
  longitude: number;
  estimatedArrival: string;
  minutesFromNow: number;
  travelMinutes: number;
}

export interface StopMarker {
  stopId: number;
  name: string;
  latitude: number;
  longitude: number;
}

export interface EtaResult {
  routeId: number;
  calculatedAt: string;
  currentStopAddress: string;
  remainingStopsCount: number;
  averageDwellMinutes: number;
  stops: StopEta[];
}

export interface TruckPosition {
  lat: number;
  lng: number;
}

export interface RouteInfo {
  routeId: number;
  stops: StopMarker[];
}
