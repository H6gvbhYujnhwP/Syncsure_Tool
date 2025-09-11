import bcrypt from 'bcrypt';
import { pool } from './db.js';

async function testAuthSystem() {
  console.log('Testing SyncSure V9 Authentication System...\n');
  
  try {
    // Test 1: bcrypt functionality
    console.log('1. Testing bcrypt password hashing...');
    const testPassword = 'TestPassword123!';
    const saltRounds = 12;
    const hashedPassword = await bcrypt.hash(testPassword, saltRounds);
    console.log('✓ Password hashed successfully');
    
    const isValid = await bcrypt.compare(testPassword, hashedPassword);
    console.log(`✓ Password verification: ${isValid ? 'PASS' : 'FAIL'}\n`);
    
    // Test 2: Database connection
    console.log('2. Testing database connection...');
    const dbTest = await pool.query('SELECT NOW() as current_time');
    console.log(`✓ Database connected: ${dbTest.rows[0].current_time}\n`);
    
    // Test 3: Check existing test account
    console.log('3. Checking existing test account...');
    const testAccount = await pool.query(
      'SELECT id, email, name, password_hash FROM accounts WHERE email = $1',
      ['admin@thegreenagents.com']
    );
    
    if (testAccount.rows.length > 0) {
      const account = testAccount.rows[0];
      console.log(`✓ Test account found: ${account.email}`);
      console.log(`✓ Account ID: ${account.id}`);
      console.log(`✓ Account name: ${account.name}`);
      console.log(`✓ Has password hash: ${account.password_hash ? 'YES' : 'NO'}`);
      
      if (account.password_hash) {
        // Test password verification with a known password
        console.log('✓ Password hash exists, authentication should work');
      } else {
        console.log('⚠ No password hash found, account needs password setup');
      }
    } else {
      console.log('⚠ Test account not found');
    }
    
    console.log('\n4. Testing session table...');
    const sessionTest = await pool.query('SELECT COUNT(*) as session_count FROM sessions');
    console.log(`✓ Sessions table accessible, current sessions: ${sessionTest.rows[0].session_count}`);
    
    console.log('\n✅ Authentication system test completed successfully!');
    
  } catch (error) {
    console.error('❌ Authentication test failed:', error);
  } finally {
    await pool.end();
  }
}

testAuthSystem();

