import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import express from 'express'
import { createApi } from './api.js'
import { openDatabase } from './db.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const dist = path.join(here, '..', 'dist')

export function createApp({ serveClient = false } = {}) {
  const db = openDatabase()
  const app = express()
  app.disable('x-powered-by')
  app.use('/api', createApi(db))

  if (serveClient && fs.existsSync(dist)) {
    app.use(express.static(dist))
    app.use((req, res, next) => {
      if (req.method !== 'GET') return next()
      return res.sendFile(path.join(dist, 'index.html'))
    })
  }

  return app
}
