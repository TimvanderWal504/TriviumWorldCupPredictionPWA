// Maps FIFA 3-letter codes to ISO 3166-1 alpha-2 for FlagCDN.
const FIFA_TO_ISO: Record<string, string> = {
  // Americas
  ARG: 'ar', BRA: 'br', URU: 'uy', CHI: 'cl', COL: 'co', ECU: 'ec',
  PER: 'pe', PAR: 'py', BOL: 'bo', VEN: 've', CAN: 'ca', USA: 'us',
  MEX: 'mx', CRC: 'cr', PAN: 'pa', JAM: 'jm', HON: 'hn', HTI: 'ht',
  CUW: 'cw', TRI: 'tt', GUY: 'gy', SUR: 'sr',
  // Europe
  FRA: 'fr', ENG: 'gb-eng', GER: 'de', ESP: 'es', POR: 'pt', NED: 'nl',
  BEL: 'be', ITA: 'it', CRO: 'hr', SUI: 'ch', DEN: 'dk', AUT: 'at',
  SCO: 'gb-sct', UKR: 'ua', TUR: 'tr', SRB: 'rs', SVK: 'sk', CZE: 'cz',
  POL: 'pl', HUN: 'hu', SLO: 'si', ALB: 'al', ROU: 'ro', GRE: 'gr',
  NOR: 'no', SWE: 'se', FIN: 'fi', ISL: 'is', WAL: 'gb-wls', NIR: 'gb-nir',
  BIH: 'ba', MNE: 'me', MKD: 'mk', GEO: 'ge', KOS: 'xk',
  // Africa
  MAR: 'ma', SEN: 'sn', EGY: 'eg', NGA: 'ng', CMR: 'cm', GHA: 'gh',
  CIV: 'ci', ALG: 'dz', TUN: 'tn', COD: 'cd', RSA: 'za', CPV: 'cv',
  // Asia / Oceania
  JPN: 'jp', KOR: 'kr', AUS: 'au', IRN: 'ir', KSA: 'sa', QAT: 'qa',
  UAE: 'ae', JOR: 'jo', IRQ: 'iq', CHN: 'cn', UZB: 'uz', IND: 'in',
  NZL: 'nz', PHI: 'ph', THA: 'th', VIE: 'vn', MAS: 'my', IDN: 'id',
  SYR: 'sy', OMA: 'om', KUW: 'kw', BHR: 'bh',
};

/** Returns a FlagCDN image URL for a FIFA 3-letter code, or '' if unknown. */
export function flagUrl(fifaCode: string, width = 80): string {
  const iso = FIFA_TO_ISO[fifaCode?.toUpperCase()];
  if (!iso) return '';
  return `https://flagcdn.com/w${width}/${iso}.png`;
}
