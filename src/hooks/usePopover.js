import { useEffect, useRef, useState } from 'react'

export function usePopover(height, width) {
  const boxRef = useRef(null)
  const [open, setOpen] = useState(false)
  const [style, setStyle] = useState({})

  useEffect(() => {
    if (!open) return undefined
    function onPointer(event) {
      if (boxRef.current && !boxRef.current.contains(event.target)) setOpen(false)
    }
    function onKey(event) {
      if (event.key === 'Escape') setOpen(false)
    }
    function onScroll(event) {
      if (boxRef.current && event.target instanceof Node && boxRef.current.contains(event.target)) return
      setOpen(false)
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    window.addEventListener('scroll', onScroll, true)
    window.addEventListener('resize', onScroll)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
      window.removeEventListener('scroll', onScroll, true)
      window.removeEventListener('resize', onScroll)
    }
  }, [open])

  function toggle() {
    if (!open && boxRef.current) {
      const rect = boxRef.current.getBoundingClientRect()
      const above = rect.top - 16
      const below = window.innerHeight - rect.bottom - 16
      const up = below < height && above > below
      const room = Math.max(160, Math.min(height, up ? above : below))
      const left = Math.max(8, Math.min(rect.right - width, window.innerWidth - width - 8))
      setStyle({
        '--pop-left': `${left}px`,
        '--pop-room': `${room}px`,
        ...(up
          ? { '--pop-bottom': `${window.innerHeight - rect.top + 8}px` }
          : { '--pop-top': `${rect.bottom + 8}px` }),
      })
    }
    setOpen(!open)
  }

  function close() {
    setOpen(false)
  }

  return { boxRef, open, style, toggle, close }
}
