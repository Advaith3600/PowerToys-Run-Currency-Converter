const fs = require('fs');
const https = require('https');
const path = 'alias.default.json';

// Function to check if a string contains numbers
const containsNumbers = (str) => /\d/.test(str);

// Function to process the key and value
const processString = (str) => str.replace(/\s+/g, '_').toLowerCase();

// Function to fetch JSON data from the endpoint
const fetchJsonData = (url, callback) => {
    https.get(url, (res) => {
        let data = '';

        res.on('data', (chunk) => {
            data += chunk;
        });

        res.on('end', () => {
            try {
                const jsonData = JSON.parse(data);
                callback(null, jsonData);
            } catch (err) {
                callback(err);
            }
        });
    }).on('error', (err) => {
        callback(err);
    });
};

const url = 'https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json';

fetchJsonData(url, (err, jsonData) => {
    if (err) {
        console.error('Error fetching the JSON data:', err);
        return;
    }

    const result = {
        rupee: 'inr',
        dollar: 'usd',
        dollars: 'usd',
        euros: 'eur',
    };

    for (const [key, value] of Object.entries(jsonData)) {
        if (!key || !value || containsNumbers(key) || containsNumbers(value)) {
            continue;
        }

        const newKey = processString(value);
        const newValue = processString(key);

        result[newKey] = newValue;
    }

    fs.writeFile(path, JSON.stringify(result, null, 2), 'utf8', (writeErr) => {
        if (writeErr) {
            console.error('Error writing the file:', writeErr);
        } else {
            console.log('File has been updated successfully.');
        }
    });
});
