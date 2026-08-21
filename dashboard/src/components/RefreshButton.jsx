export function RefreshButton({ label, onClick, variant = 'button-secondary' }) {
  return (
    <button
      aria-label={label}
      className={`${variant} button-icon`.trim()}
      onClick={onClick}
      title={label}
      type="button"
    >
      <svg aria-hidden="true" viewBox="0 0 24 24">
        <path
          d="M20 11.5a8 8 0 1 1-2.343-5.657M20 4v3.5h-3.5"
          fill="none"
          stroke="currentColor"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="1.75"
        />
      </svg>
    </button>
  )
}
