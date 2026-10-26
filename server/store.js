import { COLLECTION_KEYS } from '../src/data/defaults.js'
import { createEmptyData, normalizeSettings } from '../src/shared/normalize.js'

const MAX_PER_COLLECTION = 20000

export function createStore(db) {
  const insertUser = db.prepare('INSERT INTO users (username, password_hash, created_at) VALUES (?, ?, ?)')
  const userByName = db.prepare('SELECT id, username, password_hash FROM users WHERE username = ?')
  const insertSettings = db.prepare('INSERT INTO settings (user_id, data) VALUES (?, ?)')
  const upsertSettings = db.prepare(
    'INSERT INTO settings (user_id, data) VALUES (?, ?) ON CONFLICT(user_id) DO UPDATE SET data = excluded.data',
  )
  const selectSettings = db.prepare('SELECT data FROM settings WHERE user_id = ?')
  const insertSession = db.prepare('INSERT INTO auth_sessions (token_hash, user_id, expires_at) VALUES (?, ?, ?)')
  const sessionUser = db.prepare(
    `SELECT users.id AS id, users.username AS username, auth_sessions.expires_at AS expires_at
     FROM auth_sessions JOIN users ON users.id = auth_sessions.user_id
     WHERE auth_sessions.token_hash = ?`,
  )
  const removeSession = db.prepare('DELETE FROM auth_sessions WHERE token_hash = ?')
  const removeExpired = db.prepare('DELETE FROM auth_sessions WHERE expires_at < ?')
  const selectRecords = db.prepare('SELECT collection, data FROM records WHERE user_id = ? ORDER BY seq')
  const countRecords = db.prepare('SELECT COUNT(*) AS total FROM records WHERE user_id = ? AND collection = ?')
  const insertRecord = db.prepare('INSERT OR IGNORE INTO records (user_id, collection, id, data) VALUES (?, ?, ?, ?)')
  const updateRecord = db.prepare('UPDATE records SET data = ? WHERE user_id = ? AND collection = ? AND id = ?')
  const removeRecord = db.prepare('DELETE FROM records WHERE user_id = ? AND collection = ? AND id = ?')
  const clearCollection = db.prepare('DELETE FROM records WHERE user_id = ? AND collection = ?')

  const createUserTx = db.transaction((username, hash) => {
    const created = insertUser.run(username, hash, new Date().toISOString())
    const id = Number(created.lastInsertRowid)
    insertSettings.run(id, JSON.stringify(createEmptyData().settings))
    return id
  })

  const replaceCollectionTx = db.transaction((userId, collection, items) => {
    clearCollection.run(userId, collection)
    for (const item of items) insertRecord.run(userId, collection, item.id, JSON.stringify(item))
  })

  const replaceAllTx = db.transaction((userId, data) => {
    for (const collection of COLLECTION_KEYS) replaceCollectionTx(userId, collection, data[collection])
    upsertSettings.run(userId, JSON.stringify(data.settings))
  })

  return {
    findUser(username) {
      return userByName.get(username) || null
    },

    createUser(username, hash) {
      return createUserTx(username, hash)
    },

    createSession(userId, tokenHash, expiresAt) {
      removeExpired.run(Date.now())
      insertSession.run(tokenHash, userId, expiresAt)
    },

    findSession(tokenHash) {
      const row = sessionUser.get(tokenHash)
      if (!row) return null
      if (row.expires_at < Date.now()) {
        removeSession.run(tokenHash)
        return null
      }
      return { id: row.id, username: row.username }
    },

    deleteSession(tokenHash) {
      removeSession.run(tokenHash)
    },

    loadAll(userId) {
      const data = createEmptyData()
      for (const row of selectRecords.all(userId)) {
        if (!data[row.collection]) continue
        data[row.collection].push(JSON.parse(row.data))
      }
      const saved = selectSettings.get(userId)
      data.settings = saved ? normalizeSettings(JSON.parse(saved.data), false) : data.settings
      return data
    },

    addRecord(userId, collection, record) {
      if (countRecords.get(userId, collection).total >= MAX_PER_COLLECTION) return 'full'
      const result = insertRecord.run(userId, collection, record.id, JSON.stringify(record))
      return result.changes > 0 ? 'ok' : 'exists'
    },

    saveRecord(userId, collection, record) {
      return updateRecord.run(JSON.stringify(record), userId, collection, record.id).changes > 0
    },

    deleteRecord(userId, collection, id) {
      return removeRecord.run(userId, collection, id).changes > 0
    },

    replaceCollection(userId, collection, items) {
      replaceCollectionTx(userId, collection, items)
    },

    replaceAll(userId, data) {
      replaceAllTx(userId, data)
    },

    saveSettings(userId, settings) {
      upsertSettings.run(userId, JSON.stringify(settings))
    },
  }
}
