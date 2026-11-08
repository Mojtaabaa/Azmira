// @ts-nocheck
const ICONS = {
  home: (
    <>
      <path d="M4 10.5 12 4l8 6.5" />
      <path d="M6 9.8V20h12V9.8" />
    </>
  ),
  timer: (
    <>
      <circle cx="12" cy="13" r="7" />
      <path d="M12 13V9.5" />
      <path d="M12 13h3" />
      <path d="M9 3h6" />
    </>
  ),
  tests: (
    <>
      <rect x="6" y="3" width="12" height="18" rx="2" />
      <path d="M9 8h6" />
      <path d="M9 12h6" />
      <path d="M9 16h4" />
    </>
  ),
  mistakes: (
    <>
      <circle cx="12" cy="12" r="8" />
      <path d="m9.2 9.2 5.6 5.6" />
      <path d="m14.8 9.2-5.6 5.6" />
    </>
  ),
  chart: (
    <>
      <path d="M4 19V5" />
      <path d="M4 19h16" />
      <path d="M8 16v-4" />
      <path d="M12 16V8" />
      <path d="M16 16v-6" />
    </>
  ),
  tasks: (
    <>
      <path d="M4 7.5 5.6 9 8.5 5.8" />
      <path d="M11 7.5h9" />
      <path d="M4 12.5 5.6 14 8.5 10.8" />
      <path d="M11 12.5h9" />
      <path d="M4 17.5h4" />
      <path d="M11 17.5h9" />
    </>
  ),
  calendar: (
    <>
      <rect x="4" y="5" width="16" height="15" rx="2" />
      <path d="M8 3.5V7" />
      <path d="M16 3.5V7" />
      <path d="M4 10h16" />
    </>
  ),
  topics: (
    <>
      <path d="M6 4.5h8.5L18 8v11.5H6z" />
      <path d="M14.5 4.5V8H18" />
      <path d="M9 12h6" />
      <path d="M9 15.5h4" />
    </>
  ),
  notes: (
    <>
      <path d="M7 4h7l3 3v13H7z" />
      <path d="M14 4v3h3" />
      <path d="M10 11h5" />
      <path d="M10 14.5h4" />
    </>
  ),
  data: (
    <>
      <path d="M12 4v10" />
      <path d="m8 10 4 4 4-4" />
      <path d="M5 18h14" />
    </>
  ),
  settings: (
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 3.5v2.2" />
      <path d="M12 18.3V20.5" />
      <path d="M4.8 6.8l1.6 1.6" />
      <path d="M17.6 15.6l1.6 1.6" />
      <path d="M3.5 12h2.2" />
      <path d="M18.3 12H20.5" />
      <path d="M4.8 17.2l1.6-1.6" />
      <path d="M17.6 8.4l1.6-1.6" />
    </>
  ),
  more: (
    <>
      <circle cx="6" cy="12" r="1.4" />
      <circle cx="12" cy="12" r="1.4" />
      <circle cx="18" cy="12" r="1.4" />
    </>
  ),
  review: (
    <>
      <path d="M5 7h10" />
      <path d="M5 12h14" />
      <path d="M5 17h8" />
      <path d="M17 5l2 2-5 5" />
    </>
  ),
  mastery: (
    <>
      <path d="M12 3 15 9l6 .5-4.5 4 1.5 6.5L12 16.5 6 20l1.5-6.5L3 9.5 9 9z" />
    </>
  ),
  coach: (
    <>
      <circle cx="12" cy="9" r="4" />
      <path d="M5 20c1.5-3.5 4-5 7-5s5.5 1.5 7 5" />
      <path d="M16 4.5 18 3" />
    </>
  ),
  trophy: (
    <>
      <path d="M8 5h8v3a4 4 0 0 1-8 0V5z" />
      <path d="M8 6H5.5A2.5 2.5 0 0 0 8 8.5" />
      <path d="M16 6h2.5A2.5 2.5 0 0 1 16 8.5" />
      <path d="M10 14h4v3H10z" />
      <path d="M8 20h8" />
    </>
  ),
  avatar: (
    <>
      <circle cx="12" cy="9" r="3.5" />
      <path d="M5.5 19c1.2-3 3.5-4.5 6.5-4.5s5.3 1.5 6.5 4.5" />
    </>
  ),
  'pillar-plan': (
    <>
      <rect x="4" y="5" width="16" height="15" rx="2" />
      <path d="M8 3.5V7" />
      <path d="M16 3.5V7" />
      <path d="M4 10h16" />
      <path d="M9 14h6" />
    </>
  ),
  'pillar-learn': (
    <>
      <path d="M4 7.5 12 4l8 3.5v9L12 20l-8-3.5z" />
      <path d="M12 4v16" />
    </>
  ),
  'pillar-improve': (
    <>
      <path d="M5 17 10 10l4 3 5-8" />
      <path d="M15 5h4v4" />
    </>
  ),
  'pillar-achieve': (
    <>
      <circle cx="12" cy="12" r="7" />
      <path d="M12 8.5v4l2.5 1.5" />
    </>
  ),
}

export default function NavIcon({ name }) {
  return (
    <svg className="nav-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
      {ICONS[name] || ICONS.home}
    </svg>
  )
}
