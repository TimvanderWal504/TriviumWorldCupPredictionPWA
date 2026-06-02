export function isOnline(): boolean {
  return navigator.onLine;
}

export function requiresConnectivity(action: string): boolean {
  if (!navigator.onLine) {
    throw new Error(`"${action}" requires an internet connection. Please check your connection and try again.`);
  }
  return true;
}
