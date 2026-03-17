require('dotenv').config({ path: '/opt/gateway/.env' });
const express  = require('express');
const fs       = require('fs');
const path     = require('path');
const { execSync } = require('child_process');

const app = express();
app.use(express.json());

const SECRET    = process.env.INGEST_SECRET || 'change-me';
const KNOWLEDGE = '/opt/llm/knowledge';

const auth = (req, res, next) => {
    if (req.headers['x-ingest-secret'] !== SECRET)
        return res.status(403).json({ error: 'unauthorized' });
    next();
};

function runIngest() {
    execSync('node /opt/llm/embedder.js', { stdio: 'inherit' });
}

app.post('/upload', auth, (req, res) => {
    let docs = req.body;
    if (!Array.isArray(docs)) docs = [docs];

    const saved  = [];
    const errors = [];

    for (const doc of docs) {
        if (!doc.id || !doc.content) {
            errors.push({ id: doc.id || '?', error: 'missing id or content' });
            continue;
        }
        const dest = path.join(KNOWLEDGE, `${doc.id}.json`);
        fs.writeFileSync(dest, JSON.stringify(doc, null, 2));
        saved.push(doc.id);
    }

    if (saved.length > 0) {
        try   { runIngest(); }
        catch (e) { return res.status(500).json({ saved, errors, ingest: 'failed', detail: e.message }); }
    }

    res.json({ saved, errors, ingest: saved.length > 0 ? 'done' : 'skipped', total_in_db: fs.readdirSync(KNOWLEDGE).filter(f => f.endsWith('.json')).length});
});

app.post('/reindex', auth, (req, res) => {
    try {
        runIngest();
        const count = fs.readdirSync(KNOWLEDGE)
            .filter(f => f.endsWith('.json')).length;
        res.json({ status: 'done', files: count });
    } catch(e) {
        res.status(500).json({ error: e.message });
    }
});

app.get('/status', (req, res) => {
    const count = fs.readdirSync(KNOWLEDGE)
        .filter(f => f.endsWith('.json') && f !== 'index.json').length;
    res.json({ status: 'online', files: count });
});

app.listen(3001, '0.0.0.0', () =>
    console.log('ingest server on port 3001 — LAN / Tailscale only')
);
