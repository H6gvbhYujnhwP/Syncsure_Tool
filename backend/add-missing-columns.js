import { pool } from './db.js';
import fs from 'fs';

async function addMissingColumns() {
  try {
    console.log('Adding missing columns to accounts table...');
    
    const sql = fs.readFileSync('./add-missing-columns.sql', 'utf8');
    await pool.query(sql);
    
    console.log('✅ Missing columns added successfully');
    
    // Verify the columns were added
    const schemaQuery = `
      SELECT column_name, data_type, column_default
      FROM information_schema.columns 
      WHERE table_name = 'accounts' AND column_name IN ('status', 'subscription_status')
      ORDER BY column_name;
    `;
    
    const result = await pool.query(schemaQuery);
    console.log('\nAdded columns:');
    result.rows.forEach(row => {
      console.log(`- ${row.column_name}: ${row.data_type} (default: ${row.column_default})`);
    });
    
    // Check test account
    console.log('\nTest account status:');
    const accountQuery = `
      SELECT email, status, subscription_status
      FROM accounts 
      WHERE email = 'admin@thegreenagents.com'
    `;
    
    const accountResult = await pool.query(accountQuery);
    if (accountResult.rows.length > 0) {
      const account = accountResult.rows[0];
      console.log(`- Email: ${account.email}`);
      console.log(`- Status: ${account.status}`);
      console.log(`- Subscription Status: ${account.subscription_status}`);
    }
    
  } catch (error) {
    console.error('❌ Failed to add columns:', error);
  } finally {
    await pool.end();
  }
}

addMissingColumns();

