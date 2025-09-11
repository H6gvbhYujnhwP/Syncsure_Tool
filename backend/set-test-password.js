import bcrypt from 'bcrypt';
import { pool } from './db.js';

async function setTestPassword() {
  const testEmail = 'admin@thegreenagents.com';
  const newPassword = 'TestPassword123!';
  
  try {
    console.log('Setting password for test account...');
    
    // Hash the new password
    const saltRounds = 12;
    const passwordHash = await bcrypt.hash(newPassword, saltRounds);
    
    // Update the account with the new password
    const result = await pool.query(
      'UPDATE accounts SET password_hash = $1, updated_at = NOW() WHERE email = $2 RETURNING id, email, name',
      [passwordHash, testEmail]
    );
    
    if (result.rows.length > 0) {
      const account = result.rows[0];
      console.log('✅ Password updated successfully');
      console.log(`Account: ${account.email}`);
      console.log(`Name: ${account.name}`);
      console.log(`New password: ${newPassword}`);
      console.log('');
      console.log('You can now test login with:');
      console.log(`Email: ${testEmail}`);
      console.log(`Password: ${newPassword}`);
    } else {
      console.log('❌ Account not found');
    }
    
  } catch (error) {
    console.error('❌ Failed to set password:', error);
  } finally {
    await pool.end();
  }
}

setTestPassword();

