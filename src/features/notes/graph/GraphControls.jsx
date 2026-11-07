import { SUBJECT_COLORS } from '../../../data/subjects'
import { useGraphSettingsStore } from '../store/graphSettingsStore'
import { useNotesStore } from '../store/notesStore'

export default function GraphControls({ local = false, onAnimate }) {
  const settings = useGraphSettingsStore()
  const patch = useGraphSettingsStore((state) => state.patch)
  const folders = useNotesStore((state) => state.folders)
  const notes = useNotesStore((state) => state.notes)
  const knownTitles = new Set(notes.map((note) => note.title.trim().toLowerCase()))
  const unresolvedCount = notes.reduce((count, note) => (
    count + (note.outgoingLinks || []).filter((title) => !knownTitles.has(title.trim().toLowerCase())).length
  ), 0)
  const addGroup = useGraphSettingsStore((state) => state.addGroup)
  const removeGroup = useGraphSettingsStore((state) => state.removeGroup)

  return (
    <div className="notes-graph-controls">
      <header className="notes-graph-controls-head">
        <h2>تنظیمات گراف</h2>
        <p className="notes-muted">کلیک = باز کردن · کشیدن گره = جابه‌جایی · Shift+کلیک = پیوند</p>
        {unresolvedCount ? <p className="notes-muted">{unresolvedCount} پیوند ناتمام</p> : null}
      </header>

      <section className="notes-graph-section">
        <h3>فیلترها</h3>
        <label>
          جستجوی گره
          <input
            type="search"
            value={settings.search}
            onChange={(event) => patch({ search: event.target.value })}
            placeholder="عنوان…"
          />
        </label>
        <label>
          فیلتر پوشه
          <select value={settings.folderFilter} onChange={(event) => patch({ folderFilter: event.target.value })}>
            <option value="">همه پوشه‌ها</option>
            {folders.map((folder) => (
              <option key={folder.id} value={folder.id}>{folder.name}</option>
            ))}
          </select>
        </label>
        <label>
          فیلتر برچسب
          <input
            type="text"
            value={settings.tagFilter}
            onChange={(event) => patch({ tagFilter: event.target.value })}
            placeholder="ریاضی"
          />
        </label>
        {local ? (
          <label>
            عمق محلی
            <div className="notes-graph-range">
              <input
                type="range"
                min="1"
                max="5"
                value={settings.localDepth}
                onChange={(event) => patch({ localDepth: Number(event.target.value) })}
              />
              <span>{settings.localDepth}</span>
            </div>
          </label>
        ) : null}
        <div className="notes-graph-checks">
          <label className="notes-check">
            <input type="checkbox" checked={settings.showOrphans} onChange={(event) => patch({ showOrphans: event.target.checked })} />
            <span>یتیمان</span>
          </label>
          <label className="notes-check">
            <input
              type="checkbox"
              checked={settings.showUnresolved}
              onChange={(event) => {
                const on = event.target.checked
                patch(on ? { showUnresolved: true, existingOnly: false } : { showUnresolved: false })
              }}
            />
            <span>پیوندهای ناتمام</span>
          </label>
          <label className="notes-check">
            <input
              type="checkbox"
              checked={settings.existingOnly}
              onChange={(event) => {
                const on = event.target.checked
                patch(on ? { existingOnly: true, showUnresolved: false } : { existingOnly: false })
              }}
            />
            <span>فقط فایل‌های موجود</span>
          </label>
        </div>
      </section>

      <section className="notes-graph-section">
        <h3>نمایش</h3>
        <label>
          اندازه گره
          <div className="notes-graph-range">
            <input type="range" min="2" max="12" value={settings.nodeSize} onChange={(event) => patch({ nodeSize: Number(event.target.value) })} />
            <span>{settings.nodeSize}</span>
          </div>
        </label>
        <label>
          ضخامت یال
          <div className="notes-graph-range">
            <input type="range" min="0.4" max="4" step="0.1" value={settings.linkThickness} onChange={(event) => patch({ linkThickness: Number(event.target.value) })} />
            <span>{settings.linkThickness}</span>
          </div>
        </label>
        <label>
          آستانه متن
          <div className="notes-graph-range">
            <input type="range" min="0.1" max="2" step="0.05" value={settings.textFade} onChange={(event) => patch({ textFade: Number(event.target.value) })} />
            <span>{settings.textFade}</span>
          </div>
        </label>
        <label className="notes-check">
          <input type="checkbox" checked={settings.arrows} onChange={(event) => patch({ arrows: event.target.checked })} />
          <span>پیکان روی یال‌ها</span>
        </label>
      </section>

      <section className="notes-graph-section">
        <h3>نیروها</h3>
        <label>
          مرکز
          <div className="notes-graph-range">
            <input type="range" min="0" max="0.3" step="0.01" value={settings.centerForce} onChange={(event) => patch({ centerForce: Number(event.target.value) })} />
            <span>{settings.centerForce}</span>
          </div>
        </label>
        <label>
          دافعه
          <div className="notes-graph-range">
            <input type="range" min="-300" max="-20" value={settings.repelForce} onChange={(event) => patch({ repelForce: Number(event.target.value) })} />
            <span>{settings.repelForce}</span>
          </div>
        </label>
        <label>
          نیروی پیوند
          <div className="notes-graph-range">
            <input type="range" min="0.05" max="1" step="0.05" value={settings.linkForce} onChange={(event) => patch({ linkForce: Number(event.target.value) })} />
            <span>{settings.linkForce}</span>
          </div>
        </label>
        <label>
          فاصله پیوند
          <div className="notes-graph-range">
            <input type="range" min="20" max="140" value={settings.linkDistance} onChange={(event) => patch({ linkDistance: Number(event.target.value) })} />
            <span>{settings.linkDistance}</span>
          </div>
        </label>
        <button type="button" className="button button-primary" onClick={onAnimate}>شروع دوباره چینش</button>
      </section>

      <section className="notes-graph-section">
        <h3>گروه‌ها</h3>
        <ul className="notes-graph-groups">
          {settings.groups.map((group) => (
            <li key={group.id}>
              <button type="button" className="notes-graph-group" onClick={() => removeGroup(group.id)} title="حذف گروه">
                <span className="notes-graph-group-dot" style={{ background: group.color }} />
                <span>{group.label || group.query}</span>
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
            const color = window.prompt(`رنگ مثل ${SUBJECT_COLORS.physics}`, SUBJECT_COLORS.physics)
            if (!label || !query) return
            addGroup({ id: `g-${Date.now()}`, label, query, color: color || SUBJECT_COLORS.physics })
          }}
        >
          افزودن گروه
        </button>
      </section>
    </div>
  )
}
