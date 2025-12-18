// api/decide.js

export default function handler(request, response) {
    // 1. Get the "choice" sent from the frontend
    const { choice } = request.query;
  
    // 2. Decide the response
    let resultMessage = "";
  
    if (choice === 'accept') {
      resultMessage = "Granted";
    } else if (choice === 'deny') {
      resultMessage = "Come back next time";
    } else {
      resultMessage = "Invalid choice";
    }
  
    // 3. Send the result back to the browser
    return response.status(200).json({ 
      message: resultMessage 
    });
  }
  