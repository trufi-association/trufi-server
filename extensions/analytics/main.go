package main

import (
	"database/sql"
	"encoding/json"
	"io"
	"log"
	"net/http"
	"os"
	"time"

	_ "github.com/lib/pq"
)

type RequestLog struct {
	Method     string            `json:"method"`
	URI        string            `json:"uri"`
	Host       string            `json:"host"`
	IP         string            `json:"ip"`
	Headers    map[string]string `json:"headers"`
	Body       string            `json:"body"`
	ReceivedAt time.Time         `json:"received_at"`
}

var db *sql.DB

func main() {
	var err error

	dbURL := os.Getenv("DATABASE_URL")
	if dbURL == "" {
		dbURL = "postgres://analytics:analytics@postgres:5432/analytics?sslmode=disable"
	}

	for i := 0; i < 30; i++ {
		db, err = sql.Open("postgres", dbURL)
		if err == nil {
			err = db.Ping()
			if err == nil {
				break
			}
		}
		log.Printf("Waiting for database... (%d/30)", i+1)
		time.Sleep(time.Second)
	}

	if err != nil {
		log.Fatal("Failed to connect to database:", err)
	}
	defer db.Close()

	if err := initDB(); err != nil {
		log.Fatal("Failed to initialize database:", err)
	}

	http.HandleFunc("/log", handleLog)
	http.HandleFunc("/health", handleHealth)

	port := os.Getenv("PORT")
	if port == "" {
		port = "3000"
	}

	log.Printf("Analytics service listening on :%s", port)
	log.Fatal(http.ListenAndServe(":"+port, nil))
}

func initDB() error {
	_, err := db.Exec(`
		CREATE TABLE IF NOT EXISTS requests (
			id BIGSERIAL PRIMARY KEY,
			method VARCHAR(10) NOT NULL,
			uri TEXT NOT NULL,
			host VARCHAR(255) NOT NULL,
			ip VARCHAR(45),
			device_id VARCHAR(255),
			user_agent TEXT,
			body TEXT,
			received_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
			created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
		);

		CREATE INDEX IF NOT EXISTS idx_requests_host ON requests(host);
		CREATE INDEX IF NOT EXISTS idx_requests_received_at ON requests(received_at);
		CREATE INDEX IF NOT EXISTS idx_requests_device_id ON requests(device_id);
	`)
	return err
}

func handleLog(w http.ResponseWriter, r *http.Request) {
	body, _ := io.ReadAll(r.Body)
	defer r.Body.Close()

	deviceID := r.Header.Get("X-Device-Id")
	if deviceID == "" {
		deviceID = r.Header.Get("Device-Id")
	}

	_, err := db.Exec(`
		INSERT INTO requests (method, uri, host, ip, device_id, user_agent, body, received_at)
		VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
	`,
		r.Header.Get("X-Original-Method"),
		r.Header.Get("X-Original-URI"),
		r.Header.Get("X-Original-Host"),
		r.Header.Get("X-Real-IP"),
		deviceID,
		r.Header.Get("User-Agent"),
		string(body),
		time.Now(),
	)

	if err != nil {
		log.Printf("Error inserting request: %v", err)
		http.Error(w, "Internal error", http.StatusInternalServerError)
		return
	}

	w.WriteHeader(http.StatusOK)
}

func handleHealth(w http.ResponseWriter, r *http.Request) {
	if err := db.Ping(); err != nil {
		http.Error(w, "Database unavailable", http.StatusServiceUnavailable)
		return
	}
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]string{"status": "ok"})
}
