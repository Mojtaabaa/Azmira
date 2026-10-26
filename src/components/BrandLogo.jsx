export default function BrandLogo({ size = 40, className = '' }) {
  return (
    <img
      className={`brand-logo ${className}`.trim()}
      src="/logo.png"
      alt="رتبه"
      width={size}
      height={size}
      decoding="async"
    />
  )
}
