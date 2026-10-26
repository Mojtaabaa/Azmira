import { useGraphSettingsStore } from '../store/graphSettingsStore'

export default function GraphControls({ local = false, onAnimate }) {
  const settings = useGraphSettingsStore()
  const patch = useGraphSettingsStore((state) => state.patch)
  const addGroup = useGraphSettingsStore((state) => state.addGroup)
  const removeGroup = useGraphSettingsStore((state) => state.removeGroup)

  return (
    <div className="notes-graph-controls">
      <strong>فیلترها</strong>
      <label>
        جستجوی گره
        <input value={settings.search} onChange={(event) => patch({ search: event.target.value })} />
      </label>
      <label>
        فیلتر پوشه
        <input value={settings.folderFilter} onChange={(event) => patch({ folderFilter: event.target.value })} placeholder="شناسه یا خالی" />
      </label>
      <label>
        فیلتر برچسب
        <input value={settings.tagFilter} onChange={(event) => patch({ tagFilter: event.target.value })} placeholder="ریاضی" />
      </label>
      {local ? (
        <label>
          عمق محلی {settings.localDepth}
          <input
            type="range"
            min="1"
            max="5"
            value={settings.localDepth}
            onChange={(event) => patch({ localDepth: Number(event.target.value) })}
          />
        </label>
      ) : null}
      <label className="notes-check">
        <span><input type="checkbox" checked={settings.showOrphans} onChange={(event) => patch({ showOrphans: event.target.checked })} /> یتیمان</span>
      </label>
      <label className="notes-check">
        <span>
          <input
            type="checkbox"
            checked={settings.showUnresolved}
            onChange={(event) => {
              const on = event.target.checked
              patch(on ? { showUnresolved: true, existingOnly: false } : { showUnresolved: false })
            }}
          />
          {' '}پیوندهای ناتمام
        </span>
      </label>
      <label className="notes-check">
        <span>
          <input
            type="checkbox"
            checked={settings.existingOnly}
            onChange={(event) => {
              const on = event.target.checked
              patch(on ? { existingOnly: true, showUnresolved: false } : { existingOnly: false })
            }}
          />
          {' '}فقط فایل‌های موجود
        </span>
      </label>

      <strong>نمایش</strong>
      <label>
        اندازه گره {settings.nodeSize}
        <input type="range" min="2" max="12" value={settings.nodeSize} onChange={(event) => patch({ nodeSize: Number(event.target.value) })} />
      </label>
      <label>
        ضخامت یال {settings.linkThickness}
        <input type="range" min="0.4" max="4" step="0.1" value={settings.linkThickness} onChange={(event) => patch({ linkThickness: Number(event.target.value) })} />
      </label>
      <label>
        آستانه متن {settings.textFade}
        <input type="range" min="0.1" max="2" step="0.05" value={settings.textFade} onChange={(event) => patch({ textFade: Number(event.target.value) })} />
      </label>
      <label className="notes-check">
        <span><input type="checkbox" checked={settings.arrows} onChange={(event) => patch({ arrows: event.target.checked })} /> پیکان</span>
      </label>

      <strong>نیروها</strong>
      <label>
        مرکز {settings.centerForce}
        <input type="range" min="0" max="0.3" step="0.01" value={settings.centerForce} onChange={(event) => patch({ centerForce: Number(event.target.value) })} />
      </label>
      <label>
        دافعه {settings.repelForce}
        <input type="range" min="-300" max="-20" value={settings.repelForce} onChange={(event) => patch({ repelForce: Number(event.target.value) })} />
      </label>
      <label>
        نیروی پیوند {settings.linkForce}
        <input type="range" min="0.05" max="1" step="0.05" value={settings.linkForce} onChange={(event) => patch({ linkForce: Number(event.target.value) })} />
      </label>
      <label>
        فاصله پیوند {settings.linkDistance}
        <input type="range" min="20" max="140" value={settings.linkDistance} onChange={(event) => patch({ linkDistance: Number(event.target.value) })} />
      </label>
      <button type="button" className="button" onClick={onAnimate}>شروع دوباره چینش</button>

      <strong>گروه‌ها</strong>
      <ul className="notes-tag-list">
        {settings.groups.map((group) => (
          <li key={group.id}>
            <button type="button" onClick={() => removeGroup(group.id)}>
              <span style={{ color: group.color }}>●</span> {group.label || group.query}
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        className="button"
        onClick={() => {
          const label = window.prompt('نام گروه')
          const query = window.prompt('کوئری مثل tag:#فیزیک')
          const color = window.prompt('رنگ مثل #3d7ea6', '#3d7ea6')
          if (!label || !query) return
          addGroup({ id: `g-${Date.now()}`, label, query, color: color || '#3d7ea6' })
        }}
      >
        افزودن گروه
      </button>
    </div>
  )
}
