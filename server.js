const { UDPServer, Packet, resolveA } = require('dns2');
const dgram = require('dgram');
const fs = require('fs');
const path = require('path');
const express = require('express');
const bodyParser = require('body-parser');
const app = express();

const HTTP_PORT = 3000;

// Middleware
app.use(bodyParser.json());
app.use(express.static(path.join(__dirname, 'public'))); // For static assets

// Serve Dashboard HTML
app.get('/', (req, res) => {
  res.send(`
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8" />
      <title>OpenDNS Dashboard</title>
      <style>
        body {
          font-family: 'Segoe UI', sans-serif;
          background: #f0f4f8;
          margin: 0;
          padding: 0;
        }
        header {
          background: #1a73e8;
          color: white;
          padding: 20px;
          font-size: 24px;
          text-align: center;
        }
        main {
          padding: 20px;
          max-width: 1000px;
          margin: auto;
        }
        section {
          background: white;
          padding: 20px;
          border-radius: 8px;
          box-shadow: 0 2px 5px rgba(0,0,0,0.1);
          margin-bottom: 20px;
        }
        h2 {
          margin-top: 0;
          color: #333;
        }
        .toggle-list label {
          display: flex;
          justify-content: space-between;
          padding: 8px 0;
          border-bottom: 1px solid #eee;
        }
        .toggle-list input {
          transform: scale(1.2);
        }
        button {
          padding: 8px 14px;
          background: #1a73e8;
          color: white;
          border: none;
          border-radius: 5px;
          cursor: pointer;
        }
        input[type="text"] {
          padding: 6px;
          width: 300px;
        }
        .list-item {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 5px 0;
          border-bottom: 1px solid #eee;
        }
        .list-item button {
          background: crimson;
          padding: 4px 10px;
        }
      </style>
    </head>
    <body>
      <header>OpenDNS Filter Dashboard</header>
      <main>
        <section>
          <h2>🔧 Subcategory Controls</h2>
          <form id="subcategoryForm" class="toggle-list"></form>
          <button onclick="saveSubcategories()">💾 Save</button>
        </section>

        <section>
          <h2>🟢 Allowed IPs</h2>
          <div id="allowedIPs"></div>
          <input type="text" id="newIP" placeholder="Add IP..." />
          <button onclick="addIP()">➕ Add IP</button>
        </section>

        <section>
          <h2>🚫 Blocked Domains</h2>
          <div id="blockedDomains"></div>
        </section>
      </main>

      <script>
        async function loadConfig() {
          const res = await fetch('/api/config');
          const data = await res.json();

          // Subcategory controls
          const form = document.getElementById('subcategoryForm');
          form.innerHTML = '';
          for (const [key, value] of Object.entries(data.subcategoryConfig)) {
            const label = document.createElement('label');
            label.innerHTML = \`
              <span>\${key}</span>
              <input type="checkbox" name="\${key}" \${value ? 'checked' : ''} />
            \`;
            form.appendChild(label);
          }

          // Allowed IPs list
          const allowedIPsDiv = document.getElementById('allowedIPs');
          allowedIPsDiv.innerHTML = '';
          data.allowedIPs.forEach(ip => {
            const div = document.createElement('div');
            div.classList.add('list-item');
            div.innerHTML = \`
              <span>\${ip}</span>
              <button onclick="removeIP('\${ip}')">❌</button>
            \`;
            allowedIPsDiv.appendChild(div);
          });

          // Blocked Domains list
          const blockedDomainsDiv = document.getElementById('blockedDomains');
          blockedDomainsDiv.innerHTML = '';
          Object.entries(data.blockedDomains).forEach(([category, domains]) => {
            const categoryDiv = document.createElement('div');
            categoryDiv.innerHTML = \`<strong>\${category}</strong>\`;
            const domainList = document.createElement('ul');
            domains.forEach(domain => {
              const li = document.createElement('li');
              li.innerText = domain;
              domainList.appendChild(li);
            });
            categoryDiv.appendChild(domainList);
            blockedDomainsDiv.appendChild(categoryDiv);
          });
        }

        async function saveSubcategories() {
          const form = document.getElementById('subcategoryForm');
          const subcategoryConfig = {};
          form.querySelectorAll('input[type="checkbox"]').forEach(input => {
            subcategoryConfig[input.name] = input.checked;
          });

          await fetch('/api/save-subcategories', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ subcategoryConfig })
          });

          alert('Settings saved!');
        }

        async function addIP() {
          const newIP = document.getElementById('newIP').value;
          if (!newIP) return;
          
          await fetch('/api/add-ip', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ip: newIP })
          });

          document.getElementById('newIP').value = '';
          loadConfig();
        }

        async function removeIP(ip) {
          await fetch('/api/remove-ip', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ip })
          });
          loadConfig();
        }

        // Load initial configuration
        loadConfig();
      </script>
    </body>
    </html>
  `);
});

// API Endpoints to serve and save data
app.get('/api/config', (req, res) => {
  res.json({
    subcategoryConfig, // Loaded subcategory settings
    allowedIPs: Array.from(allowedIPs), // List of allowed IPs
    blockedDomains // Blocked domains per category
  });
});

app.post('/api/save-subcategories', (req, res) => {
  const { subcategoryConfig: newConfig } = req.body;
  subcategoryConfig = newConfig;
  fs.writeFileSync(subcategoryConfigPath, JSON.stringify(subcategoryConfig, null, 2));
  res.send({ status: 'success' });
});

app.post('/api/add-ip', (req, res) => {
  const { ip } = req.body;
  allowedIPs.add(ip);
  fs.writeFileSync(allowedIPsPath, JSON.stringify(Array.from(allowedIPs), null, 2));
  res.send({ status: 'success' });
});

app.post('/api/remove-ip', (req, res) => {
  const { ip } = req.body;
  allowedIPs.delete(ip);
  fs.writeFileSync(allowedIPsPath, JSON.stringify(Array.from(allowedIPs), null, 2));
  res.send({ status: 'success' });
});

// Create a udp4 socket explicitly
const socket = dgram.createSocket('udp4');

const dataDir = path.join(__dirname, 'data');
const blockedDomainsPath = path.join(dataDir, 'blocked_DNS.json');
const allowedIPsPath = path.join(dataDir, 'allowed_IPs.json');
const subcategoryConfigPath = path.join(dataDir, 'subcategory_config.json');

// Helper to safely load JSON
function loadJson(filePath, defaultValue = {}) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (err) {
    console.error(`❌ Failed to load ${path.basename(filePath)}:`, err.message);
    return defaultValue;
  }
}

// Load lists
let blockedDomains = loadJson(blockedDomainsPath, {}); // Object: {subcategory: [domains]}
let allowedIPs = loadJson(allowedIPsPath, new Set()); // Set of IPs
let subcategoryConfig = loadJson(subcategoryConfigPath, {}); // Object: {subcategory: boolean}

// Watch for changes
fs.watchFile(blockedDomainsPath, () => {
  blockedDomains = loadJson(blockedDomainsPath, {});
  console.log('🔁 Blocked DNS list reloaded');
});

fs.watchFile(allowedIPsPath, () => {
  allowedIPs = loadJson(allowedIPsPath, new Set());
  console.log('🔁 Allowed IP list reloaded');
});

fs.watchFile(subcategoryConfigPath, () => {
  subcategoryConfig = loadJson(subcategoryConfigPath, {});
  console.log('🔁 Subcategory config reloaded');
});

// Utility: Check if domain matches blocked list or disabled subcategory
function isBlocked(domain) {
  const domainLower = domain.toLowerCase();
  const parts = domainLower.split('.');

  // Check each subcategory in blocked_DNS.json
  for (const [subcategory, domains] of Object.entries(blockedDomains)) {
    // Skip if subcategory is enabled (true or undefined)
    if (subcategoryConfig[subcategory] !== false) continue;

    // Check each domain in the subcategory
    for (const blockedDomain of domains) {
      const blockedLower = blockedDomain.toLowerCase();
      // Handle wildcard or exact match
      if (blockedLower.startsWith('*.')) {
        const wildcardDomain = blockedLower.slice(2); // Remove '*.'
        const wildcardParts = wildcardDomain.split('.');
        let match = true;
        for (let i = 0; i < wildcardParts.length; i++) {
          if (wildcardParts[wildcardParts.length - 1 - i] !== parts[parts.length - 1 - i]) {
            match = false;
            break;
          }
        }
        if (match) {
          console.log(`🚫 Blocked by wildcard in subcategory ${subcategory}: ${domain}`);
          return true;
        }
      } else if (blockedLower === domainLower) {
        console.log(`🚫 Blocked by exact match in subcategory ${subcategory}: ${domain}`);
        return true;
      }
    }
  }

  return false;
}

// Create DNS server with pre-created socket
const server = new UDPServer({
  handle: async (request, send, rinfo) => {
    const requester = rinfo.address;
    if (!allowedIPs.has(requester)) {
      console.log(`⛔ Unauthorized DNS request from ${requester}`);
      return;
    }

    const [question] = request.questions;
    if (!question || question.type !== Packet.TYPE.A) {
      console.log(`❓ Skipped non-A record query from ${requester}`);
      return send(Packet.createResponseFromRequest(request, { answers: [] }));
    }

    const { name } = question;
    console.log(`🔍 DNS Query: ${name} from ${requester}`);

    if (isBlocked(name)) {
      return send(Packet.createResponseFromRequest(request, {
        answers: [{
          name,
          type: Packet.TYPE.A,
          class: Packet.CLASS.IN,
          ttl: 300,
          address: '0.0.0.0'
        }]
      }));
    }

    try {
      const result = await resolveA(name, { dns: '8.8.8.8' });
      return send(Packet.createResponseFromRequest(request, {
        answers: result.answers
      }));
    } catch (err) {
      console.error(`❌ DNS resolution failed for ${name}:`, err.message);
      return send(Packet.createResponseFromRequest(request, {
        answers: []
      }));
    }
  }
});

// Start the server
const PORT = 5353;
server.listen(PORT, '0.0.0.0');
console.log(`📂 Blocked domains loaded: ${Object.keys(blockedDomains).length} subcategories`);
console.log(`📂 Allowed IPs loaded: ${allowedIPs.size}`);
console.log(`📂 Subcategory config loaded: ${Object.keys(subcategoryConfig).length} settings`);
console.log(`📂 DataDir: ${dataDir}`);
console.log(`🔒 Only requests from allowed IPs will be processed`);
console.log(`🔍 DNS queries will be resolved using Google DNS`);
console.log(`🚀 OpenDNS running locally on UDP port ${PORT}`);

// Start the Web Server
app.listen(HTTP_PORT, () => {
  console.log(`🚀 OpenDNS Web Interface running at http://localhost:${HTTP_PORT}`);
});