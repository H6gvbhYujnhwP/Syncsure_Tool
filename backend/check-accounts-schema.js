import { pool } from './db.js';

async function checkAccountsSchema() {
  try {
    console.log('Checking accounts table schema...');
    
    // Get table schema
    const schemaQuery = `
      SELECT column_name, data_type, is_nullable, column_default
      FROM information_schema.columns 
      WHERE table_name = 'accounts' 
      ORDER BY ordinal_position;
    `;
    
    const result = await pool.query(schemaQuery);
    
    console.log('Accounts table columns:');
    result.rows.forEach(row => {
      console.log(`- ${row.column_name}: ${row.data_type} (nullable: ${row.is_nullable})`);
    });
    
    // Check if specific columns exist
    const requiredColumns = ['status', 'subscription_status'];
    console.log('\nChecking required columns for session login:');
    
    for (const column of requiredColumns) {
      const columnExists = result.rows.some(row => row.column_name === column);
      console.log(`- ${column}: ${columnExists ? '✅ EXISTS' : '❌ MISSING'}`);
    }
    
    // Check test account data
    console.log('\nTest account data:');
    const accountQuery = `
      SELECT id, email, name, status, subscription_status, created_at
      FROM accounts 
      WHERE email = 'admin@thegreenagents.com'
    `;
    
    const accountResult = await pool.query(accountQuery);
    if (accountResult.rows.length > 0) {
      const account = accountResult.rows[0];
      console.log('Account details:');
      Object.entries(account).forEach(([key, value]) => {
        console.log(`- ${key}: ${value}`);
      });
    } else {
      console.log('❌ Test account not found');
    }
    
  } catch (error) {
    console.error('❌ Schema check failed:', error);
  } finally {
    await pool.end();
  }
}

checkAccountsSchema();

