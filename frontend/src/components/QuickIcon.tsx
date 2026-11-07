// @ts-nocheck
const PATHS = {
  today: 'M12 4v2M12 18v2M4 12h2M18 12h2M6.3 6.3l1.4 1.4M16.3 16.3l1.4 1.4M6.3 17.7l1.4-1.4M16.3 7.7l1.4-1.4M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7z',
  yesterday: 'M4 12a8 8 0 1 0 2.5-5.8M4 5v4h4M12 8v4l3 2',
  tomorrow: 'M5 17h14M7.5 17a4.5 4.5 0 0 1 9 0M12 6v3M5.6 9.6l1.8 1.8M18.4 9.6l-1.8 1.8',
  weekend: 'M6 11V8.5A2.5 2.5 0 0 1 8.5 6h7A2.5 2.5 0 0 1 18 8.5V11M4 11.5a1.5 1.5 0 0 1 3 0V14h10v-2.5a1.5 1.5 0 0 1 3 0V17H4v-5.5zM6 17v2M18 17v2',
  next: 'M5 5h14v14H5zM9 12h6M12.5 9 15.5 12l-3 3',
  none: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM6.5 17.5l11-11',
}

export default function QuickIcon({ kind }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="pick-quick-icon">
      <path d={PATHS[kind]} />
    </svg>
  )
}
