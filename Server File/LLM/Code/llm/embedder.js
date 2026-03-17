const axios = require('axios');
const fs    = require('fs');
const path  = require('path');

const OLLAMA      = "http://127.0.0.1:11434";
const EMBED_MODEL = "nomic-embed-text";
const KNOWLEDGE   = '/opt/llm/knowledge';
const INDEX_OUT   = '/opt/llm/index.json';

async function run() {
    const files = fs.readdirSync(KNOWLEDGE)
        .filter(f => f.endsWith('.json') && f !== 'index.json');

    const docs = [];
    for (const file of files) {
        const doc = JSON.parse(fs.readFileSync(path.join(KNOWLEDGE, file), 'utf8'));
        process.stdout.write(`embedding: ${doc.title || file} ... `);
        const { data } = await axios.post(`${OLLAMA}/api/embeddings`, {
            model: EMBED_MODEL, prompt: doc.content
        });
        docs.push({ id: doc.id, content: doc.content, embedding: data.embedding });
        console.log("done");
    }

    fs.writeFileSync(INDEX_OUT, JSON.stringify(docs, null, 2));
    console.log(`wrote ${docs.length} docs to ${INDEX_OUT}`);
}

run().catch(console.error);
