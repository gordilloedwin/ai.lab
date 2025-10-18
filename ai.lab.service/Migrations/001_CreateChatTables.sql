-- Migration: Create Chat System Tables
-- Description: Multi-user chat rooms with AI participant, presence tracking, and read receipts
-- Date: 2025-10-16

-- users table is assumed to exist with at least (email, name, avatar_uri)
-- ai_lab_db.users definition

CREATE TABLE IF NOT EXISTS `users` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `email` varchar(255) NOT NULL,
  `name` varchar(255) DEFAULT NULL,
  `password_hash` varchar(255) DEFAULT NULL,
  `avatar_uri` text DEFAULT NULL,
  `is_admin` tinyint(1) DEFAULT 0,
  `last_seen` datetime DEFAULT NULL,
  `created_at` datetime DEFAULT current_timestamp(),
  `context_json` longtext DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `email` (`email`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;


INSERT INTO ai_lab_db.users
(id, email, name, password_hash, avatar_uri, is_admin, last_seen, created_at, context_json)
VALUES
(1, 'admin@ai.lab', 'Administrator', 'ZcvcRoV++eiyOthbZy9lEhxAOs+kjc0kVJs1Yt8RHV+4m4C0AbAblcNPl9wwlIYt', NULL, 1, NULL, '2025-10-08 20:54:49.000', NULL);

INSERT INTO ai_lab_db.users
(id, email, name, password_hash, avatar_uri, is_admin, last_seen, created_at, context_json)
VALUES
(2, 'gordilloedwin@hotmail.com', 'edwin', '1vbK+5vk1pLjaJ350R7YH8iySzhGLHPpNNaLCEfVvT/yAUr5ClBT3gmHmj8Wpfi2', NULL, 1, '2025-10-09 17:32:16.000', '2025-10-08 20:55:59.000', NULL);

-- ai_lab_db.chunk_embeddings definition

CREATE TABLE IF NOT EXISTS chat_chunk_embeddings (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `chunk_id` varchar(255) DEFAULT NULL,
  `chunk_text` text DEFAULT NULL,
  `file_name` varchar(255) DEFAULT NULL,
  `tags` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`tags`)),
  `embedding` vector(4096) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;

-- =====================================================
-- 1. Chat Rooms Table
-- =====================================================
CREATE TABLE IF NOT EXISTS chat_rooms (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    created_by_email VARCHAR(255) NOT NULL,
    ai_model VARCHAR(100) NOT NULL DEFAULT 'deepseek-coder:6.7b',
    max_participants INT NOT NULL DEFAULT 30,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE,
    
    INDEX idx_created_by (created_by_email),
    INDEX idx_created_at (created_at),
    INDEX idx_active (is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 2. Chat Participants Table (Many-to-Many with Presence)
-- =====================================================
CREATE TABLE IF NOT EXISTS chat_participants (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    chat_room_id BIGINT NOT NULL,
    user_email VARCHAR(255) NOT NULL,
    joined_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    left_at DATETIME NULL,
    is_currently_connected BOOLEAN DEFAULT FALSE,
    connection_id VARCHAR(255) NULL COMMENT 'SignalR connection ID for real-time presence',
    last_seen_at DATETIME NULL,
    
    INDEX idx_chat_room (chat_room_id),
    INDEX idx_user_email (user_email),
    INDEX idx_active_participants (chat_room_id, is_currently_connected),
    INDEX idx_connection_id (connection_id),
    
    CONSTRAINT fk_chat_participants_room 
        FOREIGN KEY (chat_room_id) 
        REFERENCES chat_rooms(id) 
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 3. Chat Messages Table (User and AI messages)
-- =====================================================
CREATE TABLE IF NOT EXISTS chat_messages (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    chat_room_id BIGINT NOT NULL,
    sender_email VARCHAR(255) NULL COMMENT 'NULL for AI messages',
    sender_type ENUM('user', 'ai') NOT NULL,
    content TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    INDEX idx_chat_room (chat_room_id),
    INDEX idx_created_at (created_at),
    INDEX idx_sender (sender_email),
    INDEX idx_room_time (chat_room_id, created_at),
    
    CONSTRAINT fk_chat_messages_room 
        FOREIGN KEY (chat_room_id) 
        REFERENCES chat_rooms(id) 
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 4. Chat Read Receipts Table (Unread message tracking)
-- =====================================================
CREATE TABLE IF NOT EXISTS chat_read_receipts (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    chat_room_id BIGINT NOT NULL,
    user_email VARCHAR(255) NOT NULL,
    last_read_message_id BIGINT NOT NULL,
    read_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    UNIQUE KEY unique_user_room (chat_room_id, user_email),
    INDEX idx_user_unread (user_email, chat_room_id),
    
    CONSTRAINT fk_chat_read_receipts_room 
        FOREIGN KEY (chat_room_id) 
        REFERENCES chat_rooms(id) 
        ON DELETE CASCADE,
    
    CONSTRAINT fk_chat_read_receipts_message 
        FOREIGN KEY (last_read_message_id) 
        REFERENCES chat_messages(id) 
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Useful Views
-- =====================================================

-- View: Active participants per room
CREATE OR REPLACE VIEW vw_active_chat_participants AS
SELECT 
    cp.chat_room_id,
    cr.title AS room_title,
    cp.user_email,
    u.name AS user_name,
    u.avatar_uri,
    cp.connection_id,
    cp.joined_at,
    cp.last_seen_at,
    TIMESTAMPDIFF(SECOND, cp.joined_at, NOW()) AS time_in_room_seconds
FROM chat_participants cp
JOIN chat_rooms cr ON cp.chat_room_id = cr.id
JOIN users u ON cp.user_email COLLATE utf8mb4_uca1400_ai_ci = u.email
WHERE cp.is_currently_connected = TRUE
  AND cp.left_at IS NULL
  AND cr.is_active = TRUE;

-- View: Room statistics
CREATE OR REPLACE VIEW vw_chat_room_stats AS
SELECT 
    cr.id AS room_id,
    cr.title,
    cr.created_by_email,
    cr.ai_model,
    cr.max_participants,
    cr.created_at,
    COUNT(DISTINCT CASE WHEN cp.is_currently_connected = TRUE AND cp.left_at IS NULL THEN cp.user_email END) AS current_participant_count,
    COUNT(DISTINCT cp.user_email) AS total_unique_participants,
    COUNT(cm.id) AS total_messages,
    MAX(cm.created_at) AS last_message_at
FROM chat_rooms cr
LEFT JOIN chat_participants cp ON cr.id = cp.chat_room_id
LEFT JOIN chat_messages cm ON cr.id = cm.chat_room_id
WHERE cr.is_active = TRUE
GROUP BY cr.id, cr.title, cr.created_by_email, cr.ai_model, cr.max_participants, cr.created_at;

-- View: Unread message counts per user per room
CREATE OR REPLACE VIEW vw_unread_message_counts AS
SELECT 
    cr.id AS room_id,
    cr.title AS room_title,
    u.email AS user_email,
    u.name AS user_name,
    COALESCE(crr.last_read_message_id, 0) AS last_read_message_id,
    COUNT(cm.id) AS unread_count,
    MAX(cm.created_at) AS latest_unread_at
FROM chat_rooms cr
CROSS JOIN users u
LEFT JOIN chat_read_receipts crr ON cr.id = crr.chat_room_id AND u.email = crr.user_email COLLATE utf8mb4_unicode_ci
LEFT JOIN chat_messages cm ON cr.id = cm.chat_room_id 
    AND (crr.last_read_message_id IS NULL OR cm.id > crr.last_read_message_id)
WHERE cr.is_active = TRUE
GROUP BY cr.id, cr.title, u.email, u.name, crr.last_read_message_id
HAVING unread_count > 0;

-- =====================================================
-- Sample Data (Optional - Remove in production)
-- =====================================================

-- Insert a sample chat room (uncomment if needed)
-- INSERT INTO chat_rooms (title, created_by_email, ai_model) 
-- VALUES ('General Discussion', 'admin@ai.lab', 'deepseek-coder:6.7b');

-- =====================================================
-- Rollback Script (for testing)
-- =====================================================
-- To rollback this migration, run:
/*
DROP VIEW IF EXISTS vw_unread_message_counts;
DROP VIEW IF EXISTS vw_chat_room_stats;
DROP VIEW IF EXISTS vw_active_chat_participants;
DROP TABLE IF EXISTS chat_read_receipts;
DROP TABLE IF EXISTS chat_messages;
DROP TABLE IF EXISTS chat_participants;
DROP TABLE IF EXISTS chat_rooms;
*/
