-- Add missing columns to accounts table for V9 compatibility
ALTER TABLE accounts 
ADD COLUMN IF NOT EXISTS status VARCHAR(20) DEFAULT 'active',
ADD COLUMN IF NOT EXISTS subscription_status VARCHAR(20) DEFAULT 'active';

-- Update existing accounts to have active status
UPDATE accounts 
SET status = 'active', subscription_status = 'active' 
WHERE status IS NULL OR subscription_status IS NULL;

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS idx_accounts_status ON accounts(status);
CREATE INDEX IF NOT EXISTS idx_accounts_subscription_status ON accounts(subscription_status);

