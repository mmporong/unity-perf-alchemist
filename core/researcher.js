import { GoogleGenerativeAI } from '@google/generative-ai';
import 'dotenv/config';

export class Researcher {
  constructor(apiKey) {
    const genAI = new GoogleGenerativeAI(apiKey);
    this.model = genAI.getGenerativeModel({ model: "gemini-2.0-flash" }); // Default stable model
  }

  async proposeRefactor(currentCode, metrics, goal) {
    console.log(`[Researcher] 🧠 Analyzing code and proposing architectural refactor...`);
    
    const prompt = `
You are an expert Unity Performance Engineer.
Goal: ${goal}

Current Metrics:
- Avg Frame Time: ${metrics.frameTime}ms
- Memory Allocation: ${metrics.memory}MB

Target C# Code:
\`\`\`csharp
${currentCode}
\`\`\`

Analyze the code for performance bottlenecks (GC allocations, main thread blocking, inefficient loops).
Propose an ARCHITECTURAL refactor (e.g. implementing Job System, Object Pooling, or Data-Oriented structures).

Rules:
1. Provide ONLY the refactored C# code block.
2. Ensure the code is syntactically correct for Unity.
3. No explanations, no markdown outside the code block.
    `;

    try {
      const result = await this.model.generateContent(prompt);
      let newCode = result.response.text().trim();
      newCode = newCode.replace(/```csharp/g, '').replace(/```/g, '').trim();
      return newCode;
    } catch (e) {
      console.error(`[Researcher] ❌ AI Proposal failed: ${e.message}`);
      return currentCode;
    }
  }
}
