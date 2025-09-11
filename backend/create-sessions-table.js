import { pool } from './db.js';
import fs from 'fs';

async function createSessionsTable() {
  try {
    console.log('Creating sessions table...');
    
    const sql = fs.readFileSync('./create-sessions-table.sql', 'utf8');
    await pool.query(sql);
    
    console.log('✓ Sessions table created successfully');
    
    // Test the table
    const testResult = await pool.query('SELECT COUNT(*) as session_count FROM sessions');
    console.log(`✓ Sessions table accessible, current sessions: ${testResult.rows[0].session_count}`);
    
  } catch (error) {
    console.error('❌ Failed to create sessions table:', error);
  } finally {
    await pool.end();
  }
}

createSessionsTable();

