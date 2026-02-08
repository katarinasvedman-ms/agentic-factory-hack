#!/bin/bash
# =============================================================================
# GOLD Demo - Cosmos DB Seed Script (Managed Identity)
# =============================================================================
# Uses DefaultAzureCredential (Azure CLI login, Managed Identity, etc.)
# No keys required!
#
# Usage:
#   ./seed-cosmos-gold.sh <COSMOS_ENDPOINT>
#
# Or set environment variable:
#   export GOLD_COSMOS_ENDPOINT="https://gold-demo-cosmos.documents.azure.com:443/"
#   ./seed-cosmos-gold.sh
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Get Cosmos endpoint from arg or environment
COSMOS_ENDPOINT="${1:-$GOLD_COSMOS_ENDPOINT}"

if [ -z "$COSMOS_ENDPOINT" ]; then
    echo "❌ Error: Cosmos endpoint required"
    echo ""
    echo "Usage: $0 <COSMOS_ENDPOINT>"
    echo "  Or set GOLD_COSMOS_ENDPOINT environment variable"
    exit 1
fi

echo "🚀 GOLD Demo - Seeding Cosmos DB (Managed Identity)"
echo "   Endpoint: $COSMOS_ENDPOINT"
echo ""

# Install dependencies
pip3 install azure-cosmos azure-identity --quiet 2>/dev/null || true

# Create seed script
python3 << EOF
import json
import os
from azure.cosmos import CosmosClient
from azure.cosmos.partition_key import PartitionKey
from azure.identity import DefaultAzureCredential

ENDPOINT = "$COSMOS_ENDPOINT"
DATABASE = "FactoryOpsDB"

# Minimal data for demo - just enough to show it works
DEMO_DATA = {
    "Machines": [
        {"id": "TBM-001", "type": "TireBuildingMachine", "name": "Tire Building Machine 1", "location": "Line A", "status": "operational"},
        {"id": "TCP-001", "type": "TireCuringPress", "name": "Curing Press 1", "location": "Line A", "status": "operational"},
        {"id": "TUM-001", "type": "TireUniformityMachine", "name": "Uniformity Tester 1", "location": "QC", "status": "operational"}
    ],
    "Technicians": [
        {"id": "TECH-001", "name": "Anna Svensson", "department": "Mechanical", "skills": ["vibration_analysis", "bearing_replacement", "alignment", "mechanical_systems"], "available": True},
        {"id": "TECH-002", "name": "Erik Johansson", "department": "Electrical", "skills": ["temperature_control", "instrumentation", "electrical_systems", "plc_troubleshooting"], "available": True},
        {"id": "TECH-003", "name": "Lars Nilsson", "department": "Mechanical", "skills": ["tire_building_machine", "tension_control", "servo_systems", "drum_balancing"], "available": True}
    ],
    "PartsInventory": [
        {"id": "PART-001", "partNumber": "TBM-BRG-6220", "name": "Building Drum Bearing", "category": "bearings", "quantityInStock": 5, "reorderLevel": 2, "location": "Warehouse A"},
        {"id": "PART-002", "partNumber": "TCP-HTR-4KW", "name": "Curing Press Heater 4kW", "category": "heating", "quantityInStock": 3, "reorderLevel": 1, "location": "Warehouse B"},
        {"id": "PART-003", "partNumber": "GEN-TS-K400", "name": "Temperature Sensor K-Type", "category": "sensors", "quantityInStock": 12, "reorderLevel": 5, "location": "Warehouse A"},
        {"id": "PART-004", "partNumber": "TBM-LS-500N", "name": "Load Sensor 500N", "category": "sensors", "quantityInStock": 4, "reorderLevel": 2, "location": "Warehouse A"},
        {"id": "PART-005", "partNumber": "TBM-SRV-5KW", "name": "Servo Motor 5kW", "category": "motors", "quantityInStock": 2, "reorderLevel": 1, "location": "Warehouse B"}
    ],
    "Thresholds": [
        {"id": "THR-TBM", "machineType": "TireBuildingMachine", "temperature_max": 85, "vibration_max": 4.5, "tension_max": 500},
        {"id": "THR-TCP", "machineType": "TireCuringPress", "temperature_max": 180, "pressure_max": 200}
    ],
    "KnowledgeBase": [
        {
            "id": "KB-001", 
            "machineType": "TireBuildingMachine", 
            "faultCode": "E-101", 
            "faultType": "building_drum_vibration", 
            "description": "Excessive vibration detected in tire building drum",
            "likelyCauses": ["Bearing wear", "Drum imbalance", "Motor coupling misalignment", "Foundation loosening"],
            "severity": "High",
            "requiredParts": ["TBM-BRG-6220"],
            "requiredSkills": ["vibration_analysis", "bearing_replacement"]
        },
        {
            "id": "KB-002", 
            "machineType": "TireCuringPress", 
            "faultCode": "E-201", 
            "faultType": "curing_temperature_excessive", 
            "description": "Temperature in curing press exceeds safe operating limits",
            "likelyCauses": ["Faulty heating element", "Thermocouple calibration drift", "PLC temperature control logic error", "Cooling system obstruction"],
            "severity": "High",
            "requiredParts": ["TCP-HTR-4KW", "GEN-TS-K400"],
            "requiredSkills": ["temperature_control", "instrumentation"]
        },
        {
            "id": "KB-003", 
            "machineType": "TireBuildingMachine", 
            "faultCode": "E-102", 
            "faultType": "ply_tension_excessive", 
            "description": "Ply material tension exceeds acceptable limits",
            "likelyCauses": ["Tension roller misalignment", "Servo motor calibration drift", "Material feed rate mismatch", "Load sensor malfunction"],
            "severity": "Medium",
            "requiredParts": ["TBM-LS-500N", "TBM-SRV-5KW"],
            "requiredSkills": ["tension_control", "servo_systems"]
        },
        {
            "id": "KB-004", 
            "machineType": "TireBuildingMachine", 
            "faultCode": "E-103", 
            "faultType": "servo_motor_failure", 
            "description": "Servo motor performance degradation or failure",
            "likelyCauses": ["Motor winding failure", "Encoder malfunction", "Drive electronics failure", "Overheating"],
            "severity": "High",
            "requiredParts": ["TBM-SRV-5KW"],
            "requiredSkills": ["electrical_systems", "servo_systems"]
        }
    ]
}

def main():
    print("📦 Connecting to Cosmos DB with Managed Identity...")
    
    # Use DefaultAzureCredential - works with Azure CLI, Managed Identity, etc.
    credential = DefaultAzureCredential()
    client = CosmosClient(ENDPOINT, credential=credential)
    
    # Create database
    database = client.create_database_if_not_exists(id=DATABASE)
    print(f"✅ Database '{DATABASE}' ready")
    
    # Create containers and seed data
    for container_name, data in DEMO_DATA.items():
        # Determine partition key
        if container_name == "Machines":
            pk = "/type"
        elif container_name == "Technicians":
            pk = "/department"
        elif container_name == "PartsInventory":
            pk = "/category"
        elif container_name == "Thresholds" or container_name == "KnowledgeBase":
            pk = "/machineType"
        else:
            pk = "/id"
        
        container = database.create_container_if_not_exists(
            id=container_name,
            partition_key=PartitionKey(path=pk)
        )
        
        # Upsert data
        for item in data:
            container.upsert_item(item)
        
        print(f"✅ {container_name}: {len(data)} items seeded")
    
    print("")
    print("🎉 GOLD Demo Cosmos DB seeding complete!")
    print("")
    print("Verify in Azure Portal → Cosmos DB → Data Explorer")

if __name__ == "__main__":
    main()
EOF

echo ""
echo "✅ Seed complete. Now run Canary A + B tests."
