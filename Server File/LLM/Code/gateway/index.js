require('dotenv').config();
const express = require('express');
const axios   = require('axios');
const fs      = require('fs');

const app = express();
app.use(express.json());

const OLLAMA       = "http://127.0.0.1:11434";
const GEN_MODEL    = process.env.GEN_MODEL   || "llama3:8b-instruct-q4_K_M";
const EMBED_MODEL  = process.env.EMBED_MODEL || "nomic-embed-text";
const VECTOR_INDEX = '/opt/llm/index.json';

// ── vector store ──────────────────────────────────────────────────────────
let vectorIndex = [];

function loadIndex() {
    if (fs.existsSync(VECTOR_INDEX)) {
        vectorIndex = JSON.parse(fs.readFileSync(VECTOR_INDEX, 'utf8'));
        console.log(`loaded ${vectorIndex.length} docs from index`);
    } else {
        console.warn("no index.json found — RAG disabled until ingest runs");
    }
}

function cosineSim(a, b) {
    const dot  = a.reduce((s, v, i) => s + v * b[i], 0);
    const magA = Math.sqrt(a.reduce((s, v) => s + v * v, 0));
    const magB = Math.sqrt(b.reduce((s, v) => s + v * v, 0));
    return dot / (magA * magB);
}

async function retrieve(question, excludeId = null, n = 3) {
    if (vectorIndex.length === 0) return "";
    const { data } = await axios.post(`${OLLAMA}/api/embeddings`, {
        model: EMBED_MODEL, prompt: question
    });
    return vectorIndex
        .filter(doc => doc.id !== excludeId)
        .map(doc => ({ ...doc, score: cosineSim(data.embedding, doc.embedding) }))
        .sort((a, b) => b.score - a.score)
        .slice(0, n)
        .map(doc => doc.content)
        .join("\n\n");
}

// ── routes ────────────────────────────────────────────────────────────────
app.post('/chat', async (req, res) => {
    const { prompt, artifactId, systemInstruction, emotion, history } = req.body;

    // fetch current artifact directly if player is looking at one
    let primaryContext = "";
    if (artifactId) {
        const filePath = `/opt/llm/knowledge/${artifactId}.json`;
        if (fs.existsSync(filePath)) {
            const doc = JSON.parse(fs.readFileSync(filePath, 'utf8'));
            primaryContext = doc.content;
        }
    }

    // always run RAG across full knowledge base in parallel
    let ragContext = "";
    try { ragContext = await retrieve(prompt, artifactId, artifactId ? 1 : 3); } catch (_) {}

    // combine — current artifact gets priority, RAG fills in the rest
    const combined = primaryContext
        ? `[Current exhibit]\n${primaryContext}\n\n[Related museum knowledge]\n${ragContext}`
        : ragContext;

    const system = systemInstruction || (
        `You are a knowledgeable robot museum curator who knows every piece in this museum. ` +
        `Use the context below to answer. Do not invent information not in the context. ` +
        `Do not repeat information you have already told the visitor. ` +
        `If revisiting a topic, acknowledge it and add something new. ` +
        `The visitor seems ${emotion || "neutral"}.\n\n` +
        `Context:\n${combined || "No context available."}`
    );

    const messages = [
        { role: "system", content: system },
        ...(history || []),
        { role: "user",   content: prompt }
    ];

    res.setHeader('Content-Type',      'text/event-stream');
    res.setHeader('Cache-Control',     'no-cache');
    res.setHeader('X-Accel-Buffering', 'no');

    try {
        const ollamaRes = await axios.post(
            `${OLLAMA}/api/chat`,
            { model: GEN_MODEL, messages, stream: true },
            { responseType: 'stream' }
        );

        let buffer = "";
        ollamaRes.data.on('data', chunk => {
            buffer += chunk.toString();
            const lines = buffer.split('\n');
            buffer = lines.pop();
            for (const line of lines) {
                if (!line.trim()) continue;
                try {
                    const token = JSON.parse(line)?.message?.content || "";
                    if (token) res.write(`data: ${token}\n\n`);
                } catch (_) {}
            }
        });
        ollamaRes.data.on('end',   () => { res.write("data: [DONE]\n\n"); res.end(); });
        ollamaRes.data.on('error', () => { res.write("data: [ERROR]\n\n"); res.end(); });

    } catch (err) {
        console.error("Ollama not responding:", err.message);
        res.write("data: [ERROR]\n\n");
        res.end();
    }
});

app.get('/status', (req, res) => {
    res.json({ service: "LLM-Engine-143", status: "online", model: GEN_MODEL, docs: vectorIndex.length });
});

// watch for index.json changes and reload automatically
loadIndex();
fs.watchFile(VECTOR_INDEX, () => {
    console.log('index.json updated — reloading...');
    loadIndex();
});

app.listen(3000, '0.0.0.0', () => console.log("LLM bridge online — port 3000"));